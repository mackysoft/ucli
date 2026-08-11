using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Application.Features.Programs.Parsing;

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
            if (program.Steps[index] is not CallProgramStep { RequestPath: not null } call)
            {
                continue;
            }

            var instancePath = $"/steps/{index}/requestPath";
            var resolvedPath = ResolveReferencePath(input.ReferenceRootPath, call.RequestPath, instancePath, diagnostics);
            if (resolvedPath is null)
            {
                continue;
            }

            var readResult = await fileReader.ReadAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                diagnostics.Add(new ProgramDiagnostic(ReferenceUnavailableCode, instancePath, readResult.Error!));
                continue;
            }

            var requestResult = ParseRequest(readResult.Content!, instancePath);
            if (requestResult is not null)
            {
                diagnostics.Add(requestResult);
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(readResult.Content!);
                var canonical = Rfc8785JsonCanonicalizer.Canonicalize(document.RootElement);
                sources.Add(new ResolvedProgramSource(
                    instancePath,
                    NormalizeRelativePath(call.RequestPath),
                    ComputeSha256(canonical),
                    readResult.Content!.Length,
                    Encoding.UTF8.GetString(readResult.Content!)));
            }
            catch (Exception exception) when (exception is JsonException or JsonCanonicalizationException)
            {
                diagnostics.Add(new ProgramDiagnostic(ReferenceInvalidCode, instancePath, $"Referenced request JSON is invalid. {exception.Message}"));
            }
        }

        if (diagnostics.Count > 0)
        {
            return ProgramDefinitionResolutionResult.Failure(diagnostics);
        }

        try
        {
            var canonicalProgram = Rfc8785JsonCanonicalizer.Canonicalize(program.RootDocument);
            var programDigest = ComputeSha256(canonicalProgram);
            var manifestEntries = sources.Select(static source => new ProgramSourceManifestEntry(
                source.InstancePath,
                "request",
                source.Path,
                source.DocumentDigest,
                source.ByteLength)).ToArray();
            var manifestWithoutDigest = WriteManifest(input, programDigest, manifestEntries, includeDigest: false);
            var manifest = new ProgramSourceManifest(
                ComputeSha256(Rfc8785JsonCanonicalizer.Canonicalize(manifestWithoutDigest)),
                input.RootSource,
                input.RootPath is null ? null : NormalizePath(input.RootPath),
                input.PresetId,
                programDigest,
                manifestEntries);
            var definitionIdentity = WriteDefinitionIdentity(program.RootDocument, sources);
            var definitionDigest = ComputeSha256(Rfc8785JsonCanonicalizer.Canonicalize(definitionIdentity));
            return ProgramDefinitionResolutionResult.Success(new ResolvedProgramDefinition(program, sources, manifest, definitionDigest));
        }
        catch (JsonCanonicalizationException exception)
        {
            return ProgramDefinitionResolutionResult.Failure([
                new ProgramDiagnostic(ReferenceInvalidCode, null, $"Program JSON cannot be canonicalized. {exception.Message}"),
            ]);
        }
    }

    private static ProgramDiagnostic? ParseRequest (byte[] utf8Json, string instancePath)
    {
        try
        {
            using var requestDocument = JsonDocument.Parse(utf8Json);
            if (requestDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ProgramDiagnostic(ReferenceInvalidCode, instancePath, "Referenced request JSON root must be an object.");
            }

            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                writer.WriteNumber("protocolVersion", Contracts.Ipc.IpcProtocol.CurrentVersion);
                foreach (var property in requestDocument.RootElement.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    property.Value.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            var parsed = new Requests.Shared.Validation.Parsing.ValidateRequestJsonParser().Parse(Encoding.UTF8.GetString(buffer.WrittenSpan));
            return parsed.IsSuccess
                ? null
                : new ProgramDiagnostic(ReferenceInvalidCode, instancePath, parsed.Error!.Message);
        }
        catch (JsonException exception)
        {
            return new ProgramDiagnostic(ReferenceInvalidCode, instancePath, $"Referenced request JSON is invalid. {exception.Message}");
        }
    }

    private static string? ResolveReferencePath (string? root, string referencePath, string instancePath, List<ProgramDiagnostic> diagnostics)
    {
        if (root is null)
        {
            diagnostics.Add(new ProgramDiagnostic(ReferenceBoundaryCode, instancePath, "Program requestPath is not allowed for standard input."));
            return null;
        }

        if (Path.IsPathRooted(referencePath) || referencePath.Split('/', StringSplitOptions.None).Any(static segment => segment is "." or ".." or ""))
        {
            diagnostics.Add(new ProgramDiagnostic(ReferenceBoundaryCode, instancePath, "Program requestPath must be a non-empty relative path within the reference root."));
            return null;
        }

        var rootFullPath = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, referencePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!ProgramReferencePathResolver.TryResolveWithinRoot(rootFullPath, candidate, out var resolvedCandidate))
        {
            diagnostics.Add(new ProgramDiagnostic(ReferenceBoundaryCode, instancePath, "Program requestPath resolves outside the reference root after symbolic-link resolution."));
            return null;
        }

        return resolvedCandidate;
    }

    private static JsonElement WriteManifest (ProgramDefinitionResolutionInput input, string programDigest, IReadOnlyList<ProgramSourceManifestEntry> sources, bool includeDigest)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (includeDigest)
            {
                writer.WriteString("digest", string.Empty);
            }

            writer.WriteString("rootSource", input.RootSource.ToString().ToLowerInvariant());
            writer.WriteString("rootPath", input.RootPath is null ? null : NormalizePath(input.RootPath));
            writer.WriteString("presetId", input.PresetId);
            writer.WriteString("programDigest", programDigest);
            writer.WritePropertyName("sources");
            writer.WriteStartArray();
            foreach (var source in sources)
            {
                writer.WriteStartObject();
                writer.WriteString("instancePath", source.InstancePath);
                writer.WriteString("role", source.Role);
                writer.WriteString("path", source.Path);
                writer.WriteString("documentDigest", source.DocumentDigest);
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
                writer.WriteString("path", source.Path);
                writer.WriteString("documentDigest", source.DocumentDigest);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static string ComputeSha256 (byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string NormalizeRelativePath (string path) => path.Replace('\\', '/');

    private static string NormalizePath (string path) => Path.GetFullPath(path).Replace('\\', '/');
}
