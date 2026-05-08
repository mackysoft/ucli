using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.CodeAnalysis;

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
                var linePosition = diagnostic.Location.GetLineSpan().StartLinePosition;
                line = linePosition.Line + 1;
                column = linePosition.Character + 1;
            }

            return new CsEvalDiagnostic(
                diagnostic.Severity.ToString().ToLowerInvariant(),
                diagnostic.Id,
                diagnostic.GetMessage(),
                line,
                column);
        }

        public static CsEvalDiagnostic EntryPointError (string message)
        {
            return new CsEvalDiagnostic(
                "error",
                "UCEVAL001",
                message,
                line: null,
                column: null);
        }
    }
}
