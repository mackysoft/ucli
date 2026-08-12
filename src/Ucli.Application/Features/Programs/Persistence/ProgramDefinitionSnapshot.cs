using System.Buffers;
using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Stores the closed Program definition artifact body. </summary>
internal sealed record ProgramDefinitionSnapshot (
    Sha256Digest DefinitionDigest,
    JsonElement Program,
    ProgramDefinitionSnapshotManifest SourceManifest,
    IReadOnlyList<ProgramDefinitionSnapshotSource> Sources)
{
    public static ProgramDefinitionSnapshot FromResolved (ResolvedProgramDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ProgramDefinitionSnapshot(
            definition.DefinitionDigest,
            definition.Program.RootDocument.Clone(),
            ProgramDefinitionSnapshotManifest.FromResolved(definition.SourceManifest),
            definition.Sources.Select(ProgramDefinitionSnapshotSource.FromResolved).ToArray()).Validate();
    }

    /// <summary> Validates the artifact and recreates its fixed typed definition without external I/O. </summary>
    public ProgramDefinitionSnapshotFixedDefinition RestoreFixedDefinition ()
    {
        ValidateShape();
        var canonicalProgram = Rfc8785JsonCanonicalizer.Canonicalize(Program);
        var parsed = new ProgramJsonParser().Parse(canonicalProgram);
        if (!parsed.IsSuccess)
        {
            throw new ArgumentException("Program definition snapshot Program is not a valid Program document.");
        }

        var manifest = SourceManifest.Restore();
        if (manifest.Sources.Count != Sources.Count)
        {
            throw new ArgumentException("Program definition snapshot source documents must correspond one-to-one with the source manifest.");
        }
        var sources = Sources.Select((source, index) => source.Restore(manifest.Sources[index])).ToArray();
        ValidateManifest(manifest, canonicalProgram, sources);
        ValidateReferencedCalls(parsed.Program!, sources);
        ValidateDefinitionDigest(parsed.Program!.RootDocument, sources);
        return new ProgramDefinitionSnapshotFixedDefinition(parsed.Program.Steps, sources, manifest, DefinitionDigest);
    }

    /// <summary> Validates this closed artifact body. </summary>
    public ProgramDefinitionSnapshot Validate ()
    {
        _ = RestoreFixedDefinition();
        return this;
    }

    private void ValidateShape ()
    {
        if (DefinitionDigest is null || Program.ValueKind != JsonValueKind.Object || SourceManifest is null || Sources is null)
        {
            throw new ArgumentException("Program definition snapshot must contain its definition digest, Program, source manifest, and sources.");
        }
    }

    private static void ValidateReferencedCalls (ProgramDefinition program, IReadOnlyList<ResolvedProgramSource> sources)
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < program.Steps.Count; index++)
        {
            if (program.Steps[index] is not ReferencedCallProgramStep call)
            {
                continue;
            }
            var instancePath = $"/steps/{index}/requestPath";
            var source = sources.Where(source => source.InstancePath == instancePath && source.Path == call.RequestPath).ToArray();
            if (source.Length != 1 || !referenced.Add(instancePath))
            {
                throw new ArgumentException("Program definition snapshot referenced Calls must map exactly once to child sources.");
            }
        }
        if (referenced.Count != sources.Count)
        {
            throw new ArgumentException("Program definition snapshot contains missing, duplicate, or unreferenced child sources.");
        }
    }

    private static void ValidateManifest (ProgramSourceManifest manifest, byte[] canonicalProgram, IReadOnlyList<ResolvedProgramSource> sources)
    {
        var programDigest = Sha256Digest.Compute(canonicalProgram);
        if (manifest.ProgramDigest != programDigest || manifest.Sources.Count != sources.Count)
        {
            throw new ArgumentException("Program definition snapshot source manifest does not identify its Program and child sources.");
        }
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var entry = manifest.Sources[index];
            if (entry.InstancePath != source.InstancePath || entry.Role != "request" || entry.Path != source.Path
                || entry.DocumentDigest != source.DocumentDigest || entry.ByteLength != source.ByteLength)
            {
                throw new ArgumentException("Program definition snapshot source manifest does not match its child source.");
            }
        }
        var manifestJson = WriteManifest(manifest);
        if (manifest.Digest != Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(manifestJson)))
        {
            throw new ArgumentException("Program definition snapshot source manifest digest does not match its content.");
        }
    }

    private void ValidateDefinitionDigest (JsonElement program, IReadOnlyList<ResolvedProgramSource> sources)
    {
        var identity = WriteDefinitionIdentity(program, sources);
        if (DefinitionDigest != Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(identity)))
        {
            throw new ArgumentException("Program definition snapshot definition digest does not match its content.");
        }
    }

    private static JsonElement WriteManifest (ProgramSourceManifest manifest)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("rootSource", manifest.RootSource.ToString().ToLowerInvariant());
            writer.WriteString("rootPath", manifest.RootPath?.Value?.Replace('\\', '/'));
            writer.WriteString("presetId", manifest.PresetId);
            writer.WriteString("programDigest", manifest.ProgramDigest.ToString());
            writer.WritePropertyName("sources");
            writer.WriteStartArray();
            foreach (var source in manifest.Sources)
            {
                writer.WriteStartObject();
                writer.WriteString("instancePath", source.InstancePath);
                writer.WriteString("role", source.Role);
                writer.WriteString("path", source.Path.Value);
                writer.WriteString("documentDigest", source.DocumentDigest.ToString());
                writer.WriteNumber("byteLength", source.ByteLength);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static JsonElement WriteDefinitionIdentity (JsonElement program, IReadOnlyList<ResolvedProgramSource> sources)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("program");
            program.WriteTo(writer);
            writer.WritePropertyName("sources");
            writer.WriteStartArray();
            foreach (var source in sources)
            {
                writer.WriteStartObject();
                writer.WriteString("instancePath", source.InstancePath);
                writer.WriteString("role", "request");
                writer.WriteString("path", source.Path.Value);
                writer.WriteString("documentDigest", source.DocumentDigest.ToString());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}

/// <summary> Stores the source-manifest values using JSON-safe path values. </summary>
internal sealed record ProgramDefinitionSnapshotManifest (
    Sha256Digest Digest,
    ProgramRootSource RootSource,
    string? RootPath,
    string? PresetId,
    Sha256Digest ProgramDigest,
    IReadOnlyList<ProgramDefinitionSnapshotManifestEntry> Sources)
{
    public static ProgramDefinitionSnapshotManifest FromResolved (ProgramSourceManifest manifest) => new(
        manifest.Digest, manifest.RootSource, manifest.RootPath?.Value?.Replace('\\', '/'), manifest.PresetId, manifest.ProgramDigest,
        manifest.Sources.Select(static source => new ProgramDefinitionSnapshotManifestEntry(source.InstancePath, source.Role, source.Path.Value, source.DocumentDigest, source.ByteLength)).ToArray());

    public ProgramSourceManifest Restore () => new(Digest, RootSource, RootPath is null ? null : AbsolutePath.Parse(RootPath), PresetId, ProgramDigest,
        Sources.Select(static source => new ProgramSourceManifestEntry(source.InstancePath, source.Role, RootRelativePath.Parse(source.Path), source.DocumentDigest, source.ByteLength)).ToArray());
}

/// <summary> Stores one source-manifest entry. </summary>
internal sealed record ProgramDefinitionSnapshotManifestEntry (string InstancePath, string Role, string Path, Sha256Digest DocumentDigest, int ByteLength);

/// <summary> Stores one resolved child document and its provenance. </summary>
internal sealed record ProgramDefinitionSnapshotSource (
    JsonElement Document)
{
    public static ProgramDefinitionSnapshotSource FromResolved (ResolvedProgramSource source)
    {
        using var document = JsonDocument.Parse(source.CanonicalDocumentJson);
        return new ProgramDefinitionSnapshotSource(document.RootElement.Clone());
    }

    public ResolvedProgramSource Restore (ProgramSourceManifestEntry entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.InstancePath) || entry.Role != "request" || entry.Path is null
            || entry.DocumentDigest is null || entry.ByteLength < 1 || Document.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Program definition snapshot source must be complete.");
        }
        var canonical = Rfc8785JsonCanonicalizer.Canonicalize(Document);
        if (entry.DocumentDigest != Sha256Digest.Compute(canonical))
        {
            throw new ArgumentException("Program definition snapshot source document digest does not match its document.");
        }
        var request = ParseRequest(Document);
        return new ResolvedProgramSource(entry.InstancePath, entry.Path, entry.DocumentDigest, entry.ByteLength, Encoding.UTF8.GetString(canonical), request);
    }

    private static ValidateRequest ParseRequest (JsonElement document)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", IpcProtocol.CurrentVersion);
            foreach (var property in document.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        var result = new ValidateRequestJsonParser().Parse(Encoding.UTF8.GetString(buffer.WrittenSpan));
        return result.Request ?? throw new ArgumentException("Program definition snapshot source document is not a valid Request.");
    }
}

/// <summary> Represents the typed fixed definition restored from the immutable artifact. </summary>
internal sealed record ProgramDefinitionSnapshotFixedDefinition (
    IReadOnlyList<ProgramStep> Steps,
    IReadOnlyList<ResolvedProgramSource> Sources,
    ProgramSourceManifest SourceManifest,
    Sha256Digest DefinitionDigest);
