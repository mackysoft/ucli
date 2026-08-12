using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Persistence;

public sealed class ProgramDefinitionSnapshotTests
{
    [Theory]
    [InlineData("{\"steps\":[{\"command\":\"call\",\"timeoutMilliseconds\":101,\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Main.unity\"}}]},{\"command\":\"screenshot.game\",\"timeoutMilliseconds\":102,\"width\":640,\"height\":480}]}")]
    [InlineData("{\"steps\":[{\"command\":\"call\",\"timeoutMilliseconds\":201,\"requestPath\":\"request.json\"},{\"command\":\"ready\",\"timeoutMilliseconds\":202}]}")]
    [InlineData("{\"steps\":[{\"command\":\"call\",\"timeoutMilliseconds\":301,\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Inline.unity\"}}]},{\"command\":\"call\",\"timeoutMilliseconds\":302,\"requestPath\":\"request.json\"},{\"command\":\"play.enter\",\"timeoutMilliseconds\":303}]}")]
    [Trait("Size", "Small")]
    public async Task FromResolved_RestoresTheFixedTypedDefinitionWithoutReadingSources (string programJson)
    {
        const string requestJson = "{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Referenced.unity\"}}]}";
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-snapshot-{Guid.NewGuid():N}"));
        var reader = new RecordingReader(path => path.RelativePath.Value == "program.json"
            ? programJson
            : requestJson);
        var resolved = await ResolveAsync(reader, root);

        var snapshot = ProgramDefinitionSnapshot.FromResolved(resolved);
        var serialized = JsonSerializer.Serialize(snapshot, IpcJsonSerializerOptions.Default);
        var restoredSnapshot = JsonSerializer.Deserialize<ProgramDefinitionSnapshot>(serialized, IpcJsonSerializerOptions.Default)!;
        var fixedDefinition = restoredSnapshot.RestoreFixedDefinition();

        Assert.Equal(resolved.DefinitionDigest, fixedDefinition.DefinitionDigest);
        Assert.Equal(resolved.Program.Steps.Count, fixedDefinition.Steps.Count);
        Assert.Equal(resolved.Sources.Count, fixedDefinition.Sources.Count);
        Assert.Equal(resolved.Program.Steps.Select(GetSignature), fixedDefinition.Steps.Select(GetSignature));
        Assert.Equal(resolved.Sources.Select(static source => source.Request.Steps.Count), fixedDefinition.Sources.Select(static source => source.Request.Steps.Count));
        Assert.Equal(programJson.Contains("requestPath", StringComparison.Ordinal) ? 2 : 1, reader.Paths.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task FromResolved_IsDeterministicForEquivalentResolvedInput ()
    {
        const string programJson = "{\"steps\":[{\"command\":\"call\",\"timeoutMilliseconds\":1000,\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Main.unity\"}}]},{\"command\":\"ready\",\"timeoutMilliseconds\":1001}]}";
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-snapshot-{Guid.NewGuid():N}"));

        var first = ProgramDefinitionSnapshot.FromResolved(await ResolveAsync(new RecordingReader(_ => programJson), root));
        var second = ProgramDefinitionSnapshot.FromResolved(await ResolveAsync(new RecordingReader(_ => programJson), root));

        Assert.Equal(
            JsonSerializer.Serialize(first, IpcJsonSerializerOptions.Default),
            JsonSerializer.Serialize(second, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task FromResolved_SerializesOnlyTheDefinitionArtifactContract ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-snapshot-{Guid.NewGuid():N}"));
        var snapshot = ProgramDefinitionSnapshot.FromResolved(await ResolveAsync(new RecordingReader(_ =>
            "{\"steps\":[{\"command\":\"ready\",\"timeoutMilliseconds\":1000}]}"), root));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot, IpcJsonSerializerOptions.Default));

        Assert.Equal(["definitionDigest", "program", "sourceManifest", "sources"], json.RootElement.EnumerateObject().Select(static property => property.Name).OrderBy(static name => name));
        Assert.All(json.RootElement.GetProperty("sources").EnumerateArray(), static source =>
            Assert.Equal(["document"], source.EnumerateObject().Select(static property => property.Name)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Deserialize_RejectsAnArtifactPropertyOutsideTheClosedContract ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-snapshot-{Guid.NewGuid():N}"));
        var snapshot = ProgramDefinitionSnapshot.FromResolved(await ResolveAsync(new RecordingReader(_ =>
            "{\"steps\":[{\"command\":\"ready\",\"timeoutMilliseconds\":1000}]}"), root));
        var json = JsonNode.Parse(JsonSerializer.Serialize(snapshot, IpcJsonSerializerOptions.Default))!.AsObject();
        json["extra"] = true;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProgramDefinitionSnapshot>(json.ToJsonString(), IpcJsonSerializerOptions.Default));
    }

    [Theory]
    [InlineData(SnapshotTamper.MissingReferencedChild)]
    [InlineData(SnapshotTamper.DuplicateReferencedChild)]
    [InlineData(SnapshotTamper.MissingRequiredDocument)]
    [InlineData(SnapshotTamper.SourceDocumentDigest)]
    [InlineData(SnapshotTamper.ManifestDigest)]
    [InlineData(SnapshotTamper.ManifestProgramDigest)]
    [InlineData(SnapshotTamper.DefinitionDigest)]
    [Trait("Size", "Small")]
    public async Task Validate_RejectsTamperedChildCorrespondenceAndRequiredTypedFields (SnapshotTamper tamper)
    {
        const string programJson = "{\"steps\":[{\"command\":\"call\",\"timeoutMilliseconds\":1000,\"requestPath\":\"request.json\"}]}";
        const string requestJson = "{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Main.unity\"}}]}";
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-snapshot-{Guid.NewGuid():N}"));
        var snapshot = ProgramDefinitionSnapshot.FromResolved(await ResolveAsync(new RecordingReader(path =>
            path.RelativePath.Value == "program.json" ? programJson : requestJson), root));
        var json = JsonNode.Parse(JsonSerializer.Serialize(snapshot, IpcJsonSerializerOptions.Default))!.AsObject();

        switch (tamper)
        {
            case SnapshotTamper.MissingReferencedChild:
                json["sources"]!.AsArray().Clear();
                break;
            case SnapshotTamper.DuplicateReferencedChild:
                json["sources"]!.AsArray().Add(json["sources"]!.AsArray()[0]!.DeepClone());
                break;
            case SnapshotTamper.MissingRequiredDocument:
                json["sources"]!.AsArray()[0]!.AsObject().Remove("document");
                break;
            case SnapshotTamper.SourceDocumentDigest:
                json["sourceManifest"]!["sources"]!.AsArray()[0]!["documentDigest"] = new string('f', 64);
                break;
            case SnapshotTamper.ManifestDigest:
                json["sourceManifest"]!["digest"] = new string('f', 64);
                break;
            case SnapshotTamper.ManifestProgramDigest:
                json["sourceManifest"]!["programDigest"] = new string('f', 64);
                break;
            case SnapshotTamper.DefinitionDigest:
                json["definitionDigest"] = new string('f', 64);
                break;
            default:
                throw new InvalidOperationException("Unknown snapshot tamper.");
        }

        var tampered = JsonSerializer.Deserialize<ProgramDefinitionSnapshot>(json.ToJsonString(), IpcJsonSerializerOptions.Default)!;

        Assert.Throws<ArgumentException>(tampered.Validate);
    }

    private static async ValueTask<ResolvedProgramDefinition> ResolveAsync (RecordingReader reader, AbsolutePath root)
    {
        var receipt = await ProgramDefinitionRootFileReceipt.ReadAsync(
            reader,
            ContainedPath.Create(root, RootRelativePath.Parse("program.json")));
        var input = new FileProgramDefinitionResolutionInput(Assert.IsType<ProgramDefinitionRootFileReceiptSuccess>(receipt).Receipt);
        var result = await new ProgramDefinitionResolver(new ProgramJsonParser(), reader).ResolveAsync(input);
        return Assert.IsType<ResolvedProgramDefinition>(result.Definition);
    }

    private static string GetSignature (ProgramStep step) => step switch
    {
        InlineCallProgramStep inline => $"inline:{inline.TimeoutMilliseconds}:{inline.Request.Steps.Count}",
        ReferencedCallProgramStep referenced => $"reference:{referenced.TimeoutMilliseconds}:{referenced.RequestPath.Value}",
        ReadyProgramStep ready => $"ready:{ready.TimeoutMilliseconds}",
        RefreshProgramStep refresh => $"refresh:{refresh.TimeoutMilliseconds}",
        CompileProgramStep compile => $"compile:{compile.TimeoutMilliseconds}",
        PlayEnterProgramStep enter => $"play.enter:{enter.TimeoutMilliseconds}",
        PlayExitProgramStep exit => $"play.exit:{exit.TimeoutMilliseconds}",
        ScreenshotGameProgramStep game => $"screenshot.game:{game.TimeoutMilliseconds}:{game.Width}:{game.Height}",
        ScreenshotSceneProgramStep scene => $"screenshot.scene:{scene.TimeoutMilliseconds}",
        _ => throw new InvalidOperationException($"Unexpected Program Step type {step.GetType().Name}."),
    };

    private sealed class RecordingReader (Func<ContainedPath, string> read) : IProgramDefinitionFileReader
    {
        public List<ContainedPath> Paths { get; } = [];

        public ValueTask<ProgramDefinitionFileReadResult> ReadAsync (ContainedPath path, CancellationToken cancellationToken = default)
        {
            Paths.Add(path);
            return ValueTask.FromResult<ProgramDefinitionFileReadResult>(new ProgramDefinitionFileReadSuccess(
                Encoding.UTF8.GetBytes(read(path)),
                path.Target));
        }
    }

    public enum SnapshotTamper
    {
        MissingReferencedChild,
        DuplicateReferencedChild,
        MissingRequiredDocument,
        SourceDocumentDigest,
        ManifestDigest,
        ManifestProgramDigest,
        DefinitionDigest,
    }
}
