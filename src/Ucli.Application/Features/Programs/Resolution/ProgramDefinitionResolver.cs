using System.Buffers;
using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Resolves request documents within the Program reference root and computes stable digests. </summary>
internal sealed class ProgramDefinitionResolver : IProgramDefinitionResolver
{
    private const string ReferenceUnavailableCode = "program.referenceUnavailable";
    private const string ReferenceBoundaryCode = "program.referenceBoundary";
    private const string ReferenceInvalidCode = "program.referenceInvalid";

    private readonly IProgramJsonParser parser;
    private readonly IProgramDefinitionFileReader fileReader;

    /// <summary> Initializes a new resolver. </summary>
    public ProgramDefinitionResolver (IProgramJsonParser parser, IProgramDefinitionFileReader fileReader)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        this.fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
    }

    public async ValueTask<ProgramDefinitionResolutionResult> ResolveAsync (
        ProgramDefinitionResolutionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var parseResult = parser.Parse(input.Json);
        if (!parseResult.IsSuccess)
        {
            return ProgramDefinitionResolutionResult.Failure(parseResult.Diagnostics);
        }

        var program = parseResult.Program!;
        var diagnostics = new List<ProgramDiagnostic>();
        var sources = new List<ResolvedProgramSource>();
        for (var index = 0; index < program.Steps.Count; index++)
        {
            if (program.Steps[index] is not ReferencedCallProgramStep call)
            {
                continue;
            }

            var instancePath = $"/steps/{index}/requestPath";
            if (input.ReferenceRoot is null)
            {
                diagnostics.Add(new ProgramDiagnostic(ReferenceBoundaryCode, instancePath, "Program requestPath is not allowed for standard input."));
                continue;
            }

            var result = await fileReader.ReadAsync(
                    ContainedPath.Create(input.ReferenceRoot, call.RequestPath),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result is not ProgramDefinitionFileReadSuccess read)
            {
                diagnostics.Add(CreateReadDiagnostic(result, instancePath));
                continue;
            }

            var source = ParseSource(read.Content, call.RequestPath, instancePath);
            if (source.Source is null)
            {
                diagnostics.Add(source.Diagnostic!);
                continue;
            }

            sources.Add(source.Source);
        }

        if (diagnostics.Count > 0)
        {
            return ProgramDefinitionResolutionResult.Failure(diagnostics);
        }

        try
        {
            var programDigest = Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(program.RootDocument));
            var manifestEntries = sources.Select(static source => new ProgramSourceManifestEntry(
                source.InstancePath,
                "request",
                source.Path,
                source.DocumentDigest,
                source.ByteLength)).ToArray();
            var manifestWithoutDigest = WriteManifest(input, programDigest, manifestEntries);
            var manifest = new ProgramSourceManifest(
                Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(manifestWithoutDigest)),
                input.RootSource,
                input.RootPath,
                input.PresetId,
                programDigest,
                manifestEntries);
            var definitionDigest = Sha256Digest.Compute(
                Rfc8785JsonCanonicalizer.Canonicalize(WriteDefinitionIdentity(program.RootDocument, sources)));
            return ProgramDefinitionResolutionResult.Success(new ResolvedProgramDefinition(program, sources, manifest, definitionDigest));
        }
        catch (JsonCanonicalizationException exception)
        {
            return ProgramDefinitionResolutionResult.Failure([
                new ProgramDiagnostic(ReferenceInvalidCode, null, $"Program JSON cannot be canonicalized. {exception.Message}"),
            ]);
        }
    }

    private static ProgramDiagnostic CreateReadDiagnostic (ProgramDefinitionFileReadResult result, string instancePath)
    {
        return result switch
        {
            ProgramDefinitionFileReadOutsideBoundary => new ProgramDiagnostic(ReferenceBoundaryCode, instancePath, "Program requestPath resolves outside the reference root after symbolic-link resolution."),
            ProgramDefinitionFileReadChangedDuringRead => new ProgramDiagnostic(ReferenceUnavailableCode, instancePath, "Program definition path or file changed while it was being resolved or read."),
            ProgramDefinitionFileReadUnavailable unavailable => new ProgramDiagnostic(ReferenceUnavailableCode, instancePath, unavailable.Message),
            _ => throw new InvalidOperationException($"Unknown Program definition file read result: {result.GetType().Name}."),
        };
    }

    private static ParsedSource ParseSource (byte[] utf8Json, RootRelativePath path, string instancePath)
    {
        try
        {
            using var requestDocument = JsonDocument.Parse(utf8Json);
            if (requestDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ParsedSource.Failure(new ProgramDiagnostic(ReferenceInvalidCode, instancePath, "Referenced request JSON root must be an object."));
            }

            var parsedRequest = ParseRequest(requestDocument.RootElement);
            if (!parsedRequest.IsSuccess)
            {
                return ParsedSource.Failure(new ProgramDiagnostic(ReferenceInvalidCode, instancePath, parsedRequest.Error!.Message));
            }

            var canonical = Rfc8785JsonCanonicalizer.Canonicalize(requestDocument.RootElement);
            return ParsedSource.Success(new ResolvedProgramSource(
                instancePath,
                path,
                Sha256Digest.Compute(canonical),
                utf8Json.Length,
                Encoding.UTF8.GetString(canonical),
                parsedRequest.Request!));
        }
        catch (Exception exception) when (exception is JsonException or JsonCanonicalizationException)
        {
            return ParsedSource.Failure(new ProgramDiagnostic(ReferenceInvalidCode, instancePath, $"Referenced request JSON is invalid. {exception.Message}"));
        }
    }

    private static ValidateRequestJsonParseResult ParseRequest (JsonElement request)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", IpcProtocol.CurrentVersion);
            foreach (var property in request.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                property.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return new ValidateRequestJsonParser().Parse(Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    private static JsonElement WriteManifest (
        ProgramDefinitionResolutionInput input,
        Sha256Digest programDigest,
        IReadOnlyList<ProgramSourceManifestEntry> sources)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("rootSource", input.RootSource.ToString().ToLowerInvariant());
            writer.WriteString("rootPath", input.RootPath?.Value?.Replace('\\', '/'));
            writer.WriteString("presetId", input.PresetId);
            writer.WriteString("programDigest", programDigest.ToString());
            writer.WritePropertyName("sources");
            writer.WriteStartArray();
            foreach (var source in sources)
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

    private sealed record ParsedSource (ResolvedProgramSource? Source, ProgramDiagnostic? Diagnostic)
    {
        public static ParsedSource Success (ResolvedProgramSource source) => new(source, null);

        public static ParsedSource Failure (ProgramDiagnostic diagnostic) => new(null, diagnostic);
    }
}
