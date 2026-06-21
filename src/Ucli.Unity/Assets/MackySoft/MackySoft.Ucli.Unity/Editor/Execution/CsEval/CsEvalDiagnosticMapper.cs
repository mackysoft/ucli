using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Maps Roslyn diagnostics to uCLI eval diagnostics. </summary>
    internal static class CsEvalDiagnosticMapper
    {
        public static CsEvalDiagnostic Map (Diagnostic diagnostic)
        {
            int? line = null;
            int? column = null;
            if (diagnostic.Location.IsInSource)
            {
                var linePosition = diagnostic.Location.GetMappedLineSpan().StartLinePosition;
                line = linePosition.Line + 1;
                column = linePosition.Character + 1;
            }

            return new CsEvalDiagnostic(
                diagnostic.Severity.ToString().ToLowerInvariant(),
                diagnostic.Id,
                LimitMessage(diagnostic.GetMessage()),
                line,
                column);
        }

        public static CsEvalDiagnostic Create (
            string id,
            string message)
        {
            return new CsEvalDiagnostic(
                "error",
                id,
                LimitMessage(message),
                line: null,
                column: null);
        }

        public static IReadOnlyList<CsEvalDiagnostic> Limit (IEnumerable<CsEvalDiagnostic> diagnostics)
        {
            var items = new List<CsEvalDiagnostic>();
            var truncatedCount = 0;
            foreach (var diagnostic in diagnostics)
            {
                if (items.Count >= CsEvalSafetyLimits.MaxDiagnostics)
                {
                    truncatedCount++;
                    continue;
                }

                items.Add(new CsEvalDiagnostic(
                    diagnostic.Severity,
                    diagnostic.Id,
                    LimitMessage(diagnostic.Message),
                    diagnostic.Line,
                    diagnostic.Column));
            }

            if (truncatedCount > 0)
            {
                items.Add(new CsEvalDiagnostic(
                    "warning",
                    CsEvalDiagnosticIds.DiagnosticsTruncated,
                    $"C# eval diagnostics were truncated. Omitted diagnostics: {truncatedCount}.",
                    line: null,
                    column: null));
            }

            return items;
        }

        private static string LimitMessage (string message)
        {
            return LimitUtf8(message, CsEvalSafetyLimits.MaxDiagnosticMessageBytes);
        }

        private static string LimitUtf8 (
            string value,
            int maxBytes)
        {
            if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
            {
                return value;
            }

            var builder = new StringBuilder(value.Length);
            var bytes = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var characterBytes = Encoding.UTF8.GetByteCount(value.Substring(i, 1));
                if (bytes + characterBytes > maxBytes)
                {
                    break;
                }

                builder.Append(value[i]);
                bytes += characterBytes;
            }

            builder.Append("...");
            return builder.ToString();
        }
    }
}
