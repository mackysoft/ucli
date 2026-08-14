using System.Text;
using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Computes the CLI-owned RFC 8785 digest for the closed eval input. </summary>
internal static class EvalExecutionDigestCalculator
{
    public static Sha256Digest ComputeSourceDigest (string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Sha256Digest.Compute(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(source));
    }

    public static Sha256Digest ComputeExecutionDigest (
        string source,
        CsEvalSourceKind sourceKind,
        bool allowDangerous,
        bool allowPlayMode)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!TextVocabulary.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind));
        }

        var input = JsonSerializer.SerializeToUtf8Bytes(new
        {
            source,
            sourceKind = TextVocabulary.GetText(sourceKind),
            allowDangerous,
            allowPlayMode,
        });
        return Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(input));
    }
}
