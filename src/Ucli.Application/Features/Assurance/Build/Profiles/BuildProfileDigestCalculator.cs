using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Calculates canonical build profile digests. </summary>
internal static class BuildProfileDigestCalculator
{
    private const int Sha256ByteCount = 32;

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
            Output: new CanonicalBuildOutputPolicy(output.Kind),
            Options: new CanonicalBuildOptions(options.Development));

        var json = JsonSerializer.Serialize(canonical, SerializerOptions);
        Span<byte> hashBytes = stackalloc byte[Sha256ByteCount];
        if (!SHA256.TryHashData(Encoding.UTF8.GetBytes(json), hashBytes, out var bytesWritten)
            || bytesWritten != Sha256ByteCount)
        {
            throw new InvalidOperationException("SHA-256 hash computation failed.");
        }

        return ToLowerHex(hashBytes);
    }

    private static string ToLowerHex (ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Sha256ByteCount)
        {
            throw new ArgumentException("Digest byte count must match SHA-256 length.", nameof(bytes));
        }

        const string HexChars = "0123456789abcdef";
        Span<char> chars = stackalloc char[Sha256ByteCount * 2];
        var index = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            chars[index] = HexChars[value >> 4];
            chars[index + 1] = HexChars[value & 0x0F];
            index += 2;
        }

        return new string(chars);
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
            var paths = string.Equals(scenes.Source, BuildProfileSceneSourceValues.Explicit, StringComparison.Ordinal)
                ? scenes.Paths
                : null;
            return new CanonicalBuildScenes(scenes.Source, paths);
        }
    }

    private sealed record CanonicalBuildOutputPolicy (string Kind);

    private sealed record CanonicalBuildOptions (bool Development);
}
