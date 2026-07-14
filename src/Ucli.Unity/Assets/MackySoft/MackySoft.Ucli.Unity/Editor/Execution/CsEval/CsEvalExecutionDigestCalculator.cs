using System;
using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using Microsoft.CodeAnalysis.CSharp;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Computes stable execution digests for C# eval inputs. </summary>
    internal static class CsEvalExecutionDigestCalculator
    {
        public static Sha256Digest Compute (
            Sha256Digest sourceDigest,
            string sourceKind,
            string wrapperVersion,
            string referenceIdentity)
        {
            if (sourceDigest == null)
            {
                throw new ArgumentNullException(nameof(sourceDigest));
            }

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
            return Sha256Digest.Compute(Encoding.UTF8.GetBytes(digestInput));
        }
    }
}
