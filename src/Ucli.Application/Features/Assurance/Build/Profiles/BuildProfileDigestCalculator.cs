using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Calculates canonical build profile digests. </summary>
internal static class BuildProfileDigestCalculator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary> Calculates the canonical digest for one resolved build profile content. </summary>
    public static string Calculate (
        int schemaVersion,
        ResolvedBuildTarget target,
        ResolvedBuildScenes scenes,
        ResolvedBuildOutputPolicy output,
        ResolvedBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(scenes);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);

        var canonical = new CanonicalBuildProfile(
            SchemaVersion: schemaVersion,
            Target: target.StableName,
            Scenes: CanonicalBuildScenes.From(scenes),
            Output: new CanonicalBuildOutputPolicy(ContractLiteralCodec.ToValue(output.Kind)),
            Options: new CanonicalBuildOptions(options.Development));

        var json = JsonSerializer.Serialize(canonical, SerializerOptions);
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(json));
    }

    private sealed record CanonicalBuildProfile (
        int SchemaVersion,
        string Target,
        CanonicalBuildScenes Scenes,
        CanonicalBuildOutputPolicy Output,
        CanonicalBuildOptions Options);

    private sealed record CanonicalBuildScenes (
        string Source,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Paths)
    {
        public static CanonicalBuildScenes From (ResolvedBuildScenes scenes)
        {
            var paths = scenes.Source == BuildProfileSceneSource.Explicit
                ? scenes.Paths
                : null;
            return new CanonicalBuildScenes(ContractLiteralCodec.ToValue(scenes.Source), paths);
        }
    }

    private sealed record CanonicalBuildOutputPolicy (string Kind);

    private sealed record CanonicalBuildOptions (bool Development);
}
