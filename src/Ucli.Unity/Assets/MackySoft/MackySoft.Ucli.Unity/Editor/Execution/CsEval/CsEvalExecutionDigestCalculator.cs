using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using Microsoft.CodeAnalysis.CSharp;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Computes stable execution digests for C# eval inputs. </summary>
    internal static class CsEvalExecutionDigestCalculator
    {
        public static string Compute (
            string sourceDigest,
            string sourceKind,
            string wrapperVersion,
            string referenceIdentity)
        {
            var roslynVersion = typeof(CSharpCompilation).Assembly.GetName().Version?.ToString() ?? "unknown";
            var digestInput = string.Join(
                "\n",
                "ucli.cs.eval",
                sourceDigest,
                sourceKind,
                wrapperVersion,
                CsEvalEntryPointName.RequiredSignature,
                roslynVersion,
                referenceIdentity);
            return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(digestInput));
        }
    }
}
