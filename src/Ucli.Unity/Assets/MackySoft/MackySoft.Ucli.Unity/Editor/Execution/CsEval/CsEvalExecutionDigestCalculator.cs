using System;
using System.Text;
using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Computes the RFC 8785 execution digest for the closed eval input. </summary>
    internal static class CsEvalExecutionDigestCalculator
    {
        public static Sha256Digest Compute (
            string source,
            CsEvalSourceKind sourceKind,
            bool allowDangerous,
            bool allowPlayMode)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (sourceKind is not CsEvalSourceKind.Snippet and not CsEvalSourceKind.CompilationUnit)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "C# eval source kind must be specified.");
            }

            var input = JsonSerializer.SerializeToUtf8Bytes(new
            {
                source,
                sourceKind = sourceKind == CsEvalSourceKind.Snippet ? "snippet" : "compilationUnit",
                allowDangerous,
                allowPlayMode,
            });
            return Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(input));
        }
    }
}
