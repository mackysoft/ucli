using System.Text;
using System.Text.Json;

namespace MackySoft.Ucli.Skills.Manifests;

/// <summary> Serializes and reads canonical <c>ucli-skill.json</c> manifests. </summary>
public sealed class SkillManifestJsonSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
    };

    /// <summary> Serializes one manifest to deterministic JSON. </summary>
    /// <param name="manifest"> The manifest. </param>
    /// <returns> The serialized JSON with LF line endings and a trailing newline. </returns>
    public string Serialize (SkillManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("skillName", manifest.SkillName);
            writer.WriteString("contentDigest", manifest.ContentDigest);
            writer.WritePropertyName("hostArtifacts");
            writer.WriteStartArray();

            foreach (var artifact in manifest.HostArtifacts.OrderBy(static artifact => artifact.Host, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("host", artifact.Host);
                if (!string.IsNullOrWhiteSpace(artifact.Path))
                {
                    writer.WriteString("path", artifact.Path);
                }

                if (!string.IsNullOrWhiteSpace(artifact.Digest))
                {
                    writer.WriteString("digest", artifact.Digest);
                }

                writer.WriteString("materializedFrontmatterDigest", artifact.MaterializedFrontmatterDigest);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary> Reads one manifest from JSON text. </summary>
    /// <param name="json"> The JSON text. </param>
    /// <returns> The parsed manifest. </returns>
    /// <exception cref="JsonException"> Thrown when the JSON is invalid. </exception>
    public SkillManifest Deserialize (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var artifacts = root.GetProperty("hostArtifacts")
            .EnumerateArray()
            .Select(static element => new SkillHostArtifactManifest(
                Host: element.GetProperty("host").GetString() ?? string.Empty,
                Path: element.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null,
                Digest: element.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null,
                MaterializedFrontmatterDigest: element.GetProperty("materializedFrontmatterDigest").GetString() ?? string.Empty))
            .ToArray();

        return new SkillManifest(
            SchemaVersion: root.GetProperty("schemaVersion").GetInt32(),
            SkillName: root.GetProperty("skillName").GetString() ?? string.Empty,
            ContentDigest: root.GetProperty("contentDigest").GetString() ?? string.Empty,
            HostArtifacts: artifacts);
    }
}
