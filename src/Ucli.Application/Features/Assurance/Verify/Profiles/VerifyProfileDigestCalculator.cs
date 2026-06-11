using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Ready;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Profiles;

/// <summary> Calculates canonical verify profile digests. </summary>
internal static class VerifyProfileDigestCalculator
{
    private const int Sha256ByteCount = 32;

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
}
