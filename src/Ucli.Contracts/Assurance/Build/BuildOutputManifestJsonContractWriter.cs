using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Writes <c>output-manifest.json</c> contracts with a fixed public JSON shape. </summary>
internal sealed class BuildOutputManifestJsonContractWriter : IJsonContractWriter<BuildOutputManifestJsonContract>
{
    private static readonly JsonWriterOptions FormattedWriterOptions = new()
    {
        Indented = true,
    };

    private static readonly JsonWriterOptions CanonicalWriterOptions = new()
    {
        Indented = false,
    };

    /// <inheritdoc />
    public string Write (BuildOutputManifestJsonContract contract)
    {
        if (contract == null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        return WriteJson(
            contract.ToContent(),
            contract.ManifestDigest,
            includeManifestDigest: true,
            FormattedWriterOptions,
            ensureTrailingNewline: true);
    }

    /// <summary> Calculates the manifest digest from canonical content excluding <c>manifestDigest</c>. </summary>
    /// <param name="content"> The manifest content without <c>manifestDigest</c>. </param>
    /// <returns> The lowercase SHA-256 digest. </returns>
    public Sha256Digest CalculateManifestDigest (BuildOutputManifestContentJsonContract content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        return ComputeUtf8Sha256(WriteDigestSource(content));
    }

    /// <summary> Writes the canonical manifest digest source without <c>manifestDigest</c>. </summary>
    /// <param name="content"> The manifest content without <c>manifestDigest</c>. </param>
    /// <returns> The canonical digest source JSON. </returns>
    public string WriteDigestSource (BuildOutputManifestContentJsonContract content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        return WriteJson(
            content,
            manifestDigest: null,
            includeManifestDigest: false,
            CanonicalWriterOptions,
            ensureTrailingNewline: false);
    }

    private static string WriteJson (
        BuildOutputManifestContentJsonContract content,
        Sha256Digest? manifestDigest,
        bool includeManifestDigest,
        JsonWriterOptions writerOptions,
        bool ensureTrailingNewline)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", content.SchemaVersion);
            writer.WritePropertyName("target");
            writer.WriteStartObject();
            writer.WriteString("stableName", ContractLiteralCodec.ToValue(content.Target.StableName));
            writer.WriteString("unityBuildTarget", content.Target.UnityBuildTarget);
            writer.WriteEndObject();
            writer.WritePropertyName("entries");
            writer.WriteStartArray();
            for (var i = 0; i < content.Entries.Count; i++)
            {
                var entry = content.Entries[i];
                writer.WriteStartObject();
                writer.WriteString("id", entry.Id);
                writer.WriteString("kind", entry.Kind);
                writer.WriteString("sourcePath", entry.SourcePath);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteNumber("entryCount", content.EntryCount);
            writer.WriteNumber("fileCount", content.FileCount);
            writer.WriteNumber("totalBytes", content.TotalBytes);
            writer.WritePropertyName("files");
            writer.WriteStartArray();
            for (var i = 0; i < content.Files.Count; i++)
            {
                var file = content.Files[i];
                writer.WriteStartObject();
                writer.WriteString("entryId", file.EntryId);
                writer.WriteString("logicalPath", file.LogicalPath);
                writer.WriteString("sourcePath", file.SourcePath);
                writer.WriteString("artifactPath", file.ArtifactPath);
                writer.WriteNumber("sizeBytes", file.SizeBytes);
                writer.WriteString("sha256", file.Sha256.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (includeManifestDigest)
            {
                writer.WriteString("manifestDigest", manifestDigest!.ToString());
            }

            writer.WriteEndObject();
        }

        var json = NormalizeLineEndings(GetUtf8String(stream));
        if (ensureTrailingNewline && !json.EndsWith("\n", StringComparison.Ordinal))
        {
            json += "\n";
        }

        return json;
    }

    private static string NormalizeLineEndings (string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
    }

    private static string GetUtf8String (MemoryStream stream)
    {
        if (stream.TryGetBuffer(out var buffer))
        {
            return Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static Sha256Digest ComputeUtf8Sha256 (string text)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hash, text.AsSpan());
        return Sha256LowerHex.GetHashAndReset(hash);
    }

    private static void AppendUtf8 (
        IncrementalHash hash,
        ReadOnlySpan<char> text)
    {
        const int CharChunkSize = 256;
        const int ByteChunkSize = 1024;

        Span<byte> bytes = stackalloc byte[ByteChunkSize];
        while (!text.IsEmpty)
        {
            var charCount = Math.Min(text.Length, CharChunkSize);
            if (charCount < text.Length
                && char.IsHighSurrogate(text[charCount - 1])
                && char.IsLowSurrogate(text[charCount]))
            {
                charCount--;
            }

            var chars = text[..charCount];
            var byteCount = Encoding.UTF8.GetBytes(chars, bytes);
            hash.AppendData(bytes[..byteCount]);
            text = text[charCount..];
        }
    }
}
