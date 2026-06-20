using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Features.Assurance.Build;

/// <summary> Writes <c>build.json</c> metadata with a fixed public JSON shape. </summary>
internal sealed class BuildRunMetadataDocumentWriter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
    };

    /// <summary> Writes the build metadata JSON document. </summary>
    /// <param name="document"> The metadata fields to persist. </param>
    /// <param name="artifacts"> The artifact references embedded into <c>build.json</c>. </param>
    /// <returns> The formatted JSON document with a trailing newline. </returns>
    public string Write (
        BuildRunMetadataDocument document,
        IReadOnlyList<BuildArtifactRef> artifacts)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(artifacts);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", document.SchemaVersion);
            writer.WriteString("runId", document.RunId);
            WriteElement(writer, "project", document.Project);
            WriteElement(writer, "profile", document.Profile);
            WriteElement(writer, "runner", document.Runner);
            WriteElement(writer, "inputs", document.Input);
            WriteElement(writer, "lifecycle", document.Lifecycle);
            WriteElement(writer, "generations", document.Generations);
            WriteElement(writer, "summary", document.Summary);
            WriteElement(writer, "logs", document.Logs);
            WriteElement(writer, "output", document.Output);
            WriteElement(writer, "projectMutation", document.ProjectMutation);
            WriteArtifacts(writer, artifacts);
            WriteElement(writer, "dirtyState", document.DirtyState);
            writer.WriteEndObject();
        }

        var json = NormalizeLineEndings(GetUtf8String(stream));
        if (!json.EndsWith("\n", StringComparison.Ordinal))
        {
            json += "\n";
        }

        return json;
    }

    private static void WriteElement (
        Utf8JsonWriter writer,
        string propertyName,
        JsonElement value)
    {
        writer.WritePropertyName(propertyName);
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        value.WriteTo(writer);
    }

    private static void WriteArtifacts (
        Utf8JsonWriter writer,
        IReadOnlyList<BuildArtifactRef> artifacts)
    {
        writer.WritePropertyName("artifacts");
        writer.WriteStartObject();
        for (var i = 0; i < artifacts.Count; i++)
        {
            var artifact = artifacts[i];
            var artifactKey = ContractLiteralCodec.ToValue(artifact.Kind);
            writer.WritePropertyName(artifactKey);
            writer.WriteStartObject();
            writer.WriteString("path", artifact.Path);
            writer.WriteString("digest", artifact.Digest);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
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
}
