using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Calculates canonical verify profile digests. </summary>
internal static class VerifyProfileDigestCalculator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary> Calculates the canonical digest for one resolved profile. </summary>
    public static Sha256Digest Calculate (VerifyProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var canonical = new
        {
            source = ContractLiteralCodec.ToValue(profile.Source),
            name = profile.Name,
            path = profile.RepositoryRelativePath,
            steps = profile.Steps.Select(static step => new
            {
                kind = ContractLiteralCodec.ToValue(step.Kind),
                required = step.Required,
                effects = step.Effects.Select(static effect => ContractLiteralCodec.ToValue(effect)).ToArray(),
                readyTarget = step.ReadyTarget.HasValue
                    ? ContractLiteralCodec.ToValue(step.ReadyTarget.Value)
                    : null,
                testPlatform = step.TestPlatform.HasValue
                    ? MackySoft.Ucli.Contracts.Testing.TestRunPlatformCodec.ToValue(step.TestPlatform.Value)
                    : null,
                testFilter = step.TestFilter,
                testCategory = step.TestCategory,
                assemblyName = step.AssemblyName,
            }).ToArray(),
        };

        var json = JsonSerializer.SerializeToElement(canonical, SerializerOptions);
        return Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(json));
    }
}
