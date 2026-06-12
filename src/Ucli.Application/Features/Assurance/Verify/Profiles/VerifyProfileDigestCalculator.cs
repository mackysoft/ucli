using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Contracts.Cryptography;

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
    public static string Calculate (VerifyProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var canonical = new
        {
            source = profile.Source,
            name = profile.Name,
            path = profile.RepositoryRelativePath,
            steps = profile.Steps.Select(static step => new
            {
                kind = step.Kind,
                required = step.Required,
                effects = step.Effects,
                readyTarget = ReadyTargetCodec.ToValue(step.ReadyTarget),
                testPlatform = step.TestPlatform.HasValue
                    ? MackySoft.Ucli.Contracts.Testing.TestRunPlatformCodec.ToValue(step.TestPlatform.Value)
                    : null,
                testFilter = step.TestFilter,
                testCategory = step.TestCategory,
                assemblyName = step.AssemblyName,
            }).ToArray(),
        };

        var json = JsonSerializer.Serialize(canonical, SerializerOptions);
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(json));
    }
}
