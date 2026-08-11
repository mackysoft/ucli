using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Resolution;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Resolution;

public sealed class ProgramDefinitionResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_FileInput_UsesContainedReaderAndProducesFixedTypedDigests ()
    {
        const string program = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"requests/open.json\"}]}";
        const string request = "{\"steps\":[{\"args\":{\"path\":\"Assets/Main.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-definition-{Guid.NewGuid():N}"));
        var reader = new StubFileReader(path => path.RelativePath.Value == "program.json"
            ? new ProgramDefinitionFileReadSuccess(System.Text.Encoding.UTF8.GetBytes(program), AbsolutePath.Resolve(root, "program.json"))
            : new ProgramDefinitionFileReadSuccess(System.Text.Encoding.UTF8.GetBytes(request), AbsolutePath.Resolve(root, "requests/open.json")));
        var receipt = await CreateReceiptAsync(reader, root);

        var result = await new ProgramDefinitionResolver(new ProgramJsonParser(), reader).ResolveAsync(
            new FileProgramDefinitionResolutionInput(receipt));

        var definition = Assert.IsType<ResolvedProgramDefinition>(result.Definition);
        Assert.Equal("8c68006091e3bacf1c7a2993d4b0304005559bf73eb807893a5aaa7cebfcec22", definition.DefinitionDigest.ToString());
        Assert.Equal("3de91402d7d59534368be1b06c68963855146f8669e6e10a802fa989eb7791c9", definition.SourceManifest.ProgramDigest.ToString());
        Assert.Equal(ProgramRootSource.File, definition.SourceManifest.RootSource);
        Assert.Equal(AbsolutePath.Resolve(root, "program.json"), definition.SourceManifest.RootPath);
        Assert.Null(definition.SourceManifest.PresetId);
        Assert.Equal(
            ComputeManifestDigest(
                rootPath: NormalizePath(AbsolutePath.Resolve(root, "program.json").Value),
                rootSource: "file",
                presetId: null,
                programDigest: "3de91402d7d59534368be1b06c68963855146f8669e6e10a802fa989eb7791c9",
                [new ExpectedManifestSource(
                    "/steps/0/requestPath",
                    "requests/open.json",
                    "e3df0c26993e2ef372cf23b712acf537ffc6ef036c3c46cb39007d17337db24a",
                    84)]),
            definition.SourceManifest.Digest.ToString());
        var manifestSource = Assert.Single(definition.SourceManifest.Sources);
        Assert.Equal("request", manifestSource.Role);
        Assert.Equal("requests/open.json", manifestSource.Path.Value);
        Assert.Equal("e3df0c26993e2ef372cf23b712acf537ffc6ef036c3c46cb39007d17337db24a", manifestSource.DocumentDigest.ToString());
        Assert.Equal(84, manifestSource.ByteLength);
        var source = Assert.Single(definition.Sources);
        Assert.Equal("requests/open.json", source.Path.Value);
        Assert.Equal(84, source.ByteLength);
        Assert.NotNull(source.Request);
        Assert.Equal("{\"steps\":[{\"args\":{\"path\":\"Assets/Main.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}", source.CanonicalDocumentJson);
        Assert.Equal(["program.json", "requests/open.json"], reader.Paths.Select(static path => path.RelativePath.Value));
        Assert.Equal(root.Value, reader.Paths[1].BoundaryRoot.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_MultipleAndRepeatedRequestPaths_PreservesStepOrderAndRawByteLengths ()
    {
        const string program = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"a.json\"},{\"command\":\"call\",\"requestPath\":\"b.json\"},{\"command\":\"call\",\"requestPath\":\"a.json\"}]}";
        const string a = "{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/A.unity\"}}]}";
        const string b = "{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/B.unity\"}}]}";
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-definition-{Guid.NewGuid():N}"));
        var reader = new StubFileReader(path => path.RelativePath.Value switch
        {
            "program.json" => new ProgramDefinitionFileReadSuccess(System.Text.Encoding.UTF8.GetBytes(program), AbsolutePath.Resolve(root, "program.json")),
            "a.json" => new ProgramDefinitionFileReadSuccess(System.Text.Encoding.UTF8.GetBytes(a), AbsolutePath.Resolve(root, "a.json")),
            "b.json" => new ProgramDefinitionFileReadSuccess(System.Text.Encoding.UTF8.GetBytes(b), AbsolutePath.Resolve(root, "b.json")),
            _ => throw new Xunit.Sdk.XunitException("Unexpected path."),
        });

        var result = await new ProgramDefinitionResolver(new ProgramJsonParser(), reader).ResolveAsync(
            new FileProgramDefinitionResolutionInput(await CreateReceiptAsync(reader, root)));

        var definition = Assert.IsType<ResolvedProgramDefinition>(result.Definition);
        Assert.Equal(["a.json", "b.json", "a.json"], definition.Sources.Select(static source => source.Path.Value));
        Assert.Equal([a.Length, b.Length, a.Length], definition.Sources.Select(static source => source.ByteLength));
        Assert.Equal(["/steps/0/requestPath", "/steps/1/requestPath", "/steps/2/requestPath"], definition.Sources.Select(static source => source.InstancePath));
        Assert.Equal(
            ComputeManifestDigest(
                rootPath: NormalizePath(AbsolutePath.Resolve(root, "program.json").Value),
                rootSource: "file",
                presetId: null,
                programDigest: "0b40af6e6a5f4f26126351b5abfbe20cee1b47337ed507fa67ccca9132b3a5af",
                [
                    new ExpectedManifestSource("/steps/0/requestPath", "a.json", "53f31283c94c6c2d88f8edd6cc02971ba147858044a3c17322d53e0432754fe4", a.Length),
                    new ExpectedManifestSource("/steps/1/requestPath", "b.json", "7694887e4399cce340ace5349ddef42e9c90dd20a6879d5c47ba495551714819", b.Length),
                    new ExpectedManifestSource("/steps/2/requestPath", "a.json", "53f31283c94c6c2d88f8edd6cc02971ba147858044a3c17322d53e0432754fe4", a.Length),
                ]),
            definition.SourceManifest.Digest.ToString());
        Assert.Collection(
            definition.SourceManifest.Sources,
            source => AssertManifestSource(source, "/steps/0/requestPath", "a.json", "53f31283c94c6c2d88f8edd6cc02971ba147858044a3c17322d53e0432754fe4", a.Length),
            source => AssertManifestSource(source, "/steps/1/requestPath", "b.json", "7694887e4399cce340ace5349ddef42e9c90dd20a6879d5c47ba495551714819", b.Length),
            source => AssertManifestSource(source, "/steps/2/requestPath", "a.json", "53f31283c94c6c2d88f8edd6cc02971ba147858044a3c17322d53e0432754fe4", a.Length));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_Rfc8785EquivalentDocuments_HaveTheSameDefinitionDigest ()
    {
        const string canonicalProgram = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"request.json\"}]}";
        const string equivalentProgram = "{ \"steps\" : [ { \"requestPath\" : \"request.json\", \"command\" : \"call\" } ] }";
        const string canonicalRequest = "{\"steps\":[{\"args\":{\"path\":\"Assets/Main.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        const string equivalentRequest = "{ \"steps\" : [{ \"op\":\"ucli.scene.open\",\"kind\":\"op\",\"args\":{\"path\":\"Assets/Main.unity\"}}] }";

        var first = await ResolveOneRequestAsync(canonicalProgram, canonicalRequest);
        var second = await ResolveOneRequestAsync(equivalentProgram, equivalentRequest);

        Assert.Equal(first.DefinitionDigest, second.DefinitionDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_ChangingStepOrderReferencePathOrChildContent_ChangesDefinitionDigest ()
    {
        const string program = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"request.json\"},{\"command\":\"ready\"}]}";
        const string reorderedProgram = "{\"steps\":[{\"command\":\"ready\"},{\"command\":\"call\",\"requestPath\":\"request.json\"}]}";
        const string changedPathProgram = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"other.json\"},{\"command\":\"ready\"}]}";
        const string request = "{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Main.unity\"}}]}";
        const string changedRequest = "{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Other.unity\"}}]}";

        var baseline = await ResolveOneRequestAsync(program, request);
        var reordered = await ResolveOneRequestAsync(reorderedProgram, request);
        var changedPath = await ResolveOneRequestAsync(changedPathProgram, request);
        var changedChild = await ResolveOneRequestAsync(program, changedRequest);

        Assert.NotEqual(baseline.DefinitionDigest, reordered.DefinitionDigest);
        Assert.NotEqual(baseline.DefinitionDigest, changedPath.DefinitionDigest);
        Assert.NotEqual(baseline.DefinitionDigest, changedChild.DefinitionDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_StdinWithRequestPath_RejectsBeforeReading ()
    {
        var reader = new StubFileReader(_ => throw new Xunit.Sdk.XunitException("reader must not be called"));
        var result = await new ProgramDefinitionResolver(new ProgramJsonParser(), reader).ResolveAsync(
            new StdinProgramDefinitionResolutionInput("{\"steps\":[{\"command\":\"call\",\"requestPath\":\"request.json\"}]}"));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("program.referenceBoundary", diagnostic.Code);
        Assert.Empty(reader.Paths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_OutsideReadResult_MapsToBoundaryDiagnostic ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-definition-{Guid.NewGuid():N}"));
        const string program = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"request.json\"}]}";
        var reader = new StubFileReader(path => path.RelativePath.Value == "program.json"
            ? new ProgramDefinitionFileReadSuccess(System.Text.Encoding.UTF8.GetBytes(program), AbsolutePath.Resolve(root, "program.json"))
            : new ProgramDefinitionFileReadOutsideBoundary());
        var receipt = await CreateReceiptAsync(reader, root);
        var result = await new ProgramDefinitionResolver(new ProgramJsonParser(), reader).ResolveAsync(
            new FileProgramDefinitionResolutionInput(receipt));

        Assert.Equal("program.referenceBoundary", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_StdinWithoutReferences_ProducesFixedManifestWithNullRootFields ()
    {
        var reader = new StubFileReader(_ => throw new Xunit.Sdk.XunitException("reader must not be called"));
        var result = await new ProgramDefinitionResolver(new ProgramJsonParser(), reader).ResolveAsync(
            new StdinProgramDefinitionResolutionInput("{\"steps\":[{\"command\":\"ready\"}]}"));

        var manifest = Assert.IsType<ResolvedProgramDefinition>(result.Definition).SourceManifest;
        Assert.Equal("719371f43069967e948e4484998506def8091b0a8a133c2a22fb8458326bfd8f", manifest.ProgramDigest.ToString());
        Assert.Equal("6d112600a8e891dcdb715eb783af125ad3b0471e311807b1dc5d5bc9b5f44c1b", manifest.Digest.ToString());
        Assert.Equal(ProgramRootSource.Stdin, manifest.RootSource);
        Assert.Null(manifest.RootPath);
        Assert.Null(manifest.PresetId);
        Assert.Empty(manifest.Sources);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAsync_DecodesOneReceiptOnceAndRejectsInvalidUtf8BeforeConstructingInput ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-definition-{Guid.NewGuid():N}"));
        var reader = new StubFileReader(_ => new ProgramDefinitionFileReadSuccess([0xff], AbsolutePath.Resolve(root, "program.json")));

        var result = await ProgramDefinitionRootFileReceipt.ReadAsync(
            reader,
            ContainedPath.Create(root, RootRelativePath.Parse("program.json")));

        Assert.IsType<ProgramDefinitionRootFileReceiptInvalidUtf8>(result);
        Assert.Single(reader.Paths);
    }

    private static async ValueTask<ProgramDefinitionRootFileReceipt> CreateReceiptAsync (StubFileReader reader, AbsolutePath root)
    {
        var result = await ProgramDefinitionRootFileReceipt.ReadAsync(
            reader,
            ContainedPath.Create(root, RootRelativePath.Parse("program.json")));
        return Assert.IsType<ProgramDefinitionRootFileReceiptSuccess>(result).Receipt;
    }

    private static async ValueTask<ResolvedProgramDefinition> ResolveOneRequestAsync (string program, string request)
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-definition-{Guid.NewGuid():N}"));
        var reader = new StubFileReader(path => path.RelativePath.Value == "program.json"
            ? new ProgramDefinitionFileReadSuccess(System.Text.Encoding.UTF8.GetBytes(program), AbsolutePath.Resolve(root, "program.json"))
            : new ProgramDefinitionFileReadSuccess(System.Text.Encoding.UTF8.GetBytes(request), AbsolutePath.Resolve(root, "request.json")));
        var result = await new ProgramDefinitionResolver(new ProgramJsonParser(), reader).ResolveAsync(
            new FileProgramDefinitionResolutionInput(await CreateReceiptAsync(reader, root)));
        return Assert.IsType<ResolvedProgramDefinition>(result.Definition);
    }

    private static string ComputeManifestDigest (
        string? rootPath,
        string rootSource,
        string? presetId,
        string programDigest,
        IReadOnlyList<ExpectedManifestSource> sources)
    {
        var rootPathJson = rootPath is null ? "null" : JsonSerializer.Serialize(rootPath);
        var presetIdJson = presetId is null ? "null" : JsonSerializer.Serialize(presetId);
        var sourceJson = string.Join(",", sources.Select(static source =>
            $$"""{"byteLength":{{source.ByteLength}},"documentDigest":{{JsonSerializer.Serialize(source.DocumentDigest)}},"instancePath":{{JsonSerializer.Serialize(source.InstancePath)}},"path":{{JsonSerializer.Serialize(source.Path)}},"role":{{JsonSerializer.Serialize("request")}}}"""));
        var canonicalManifest = $$"""{"presetId":{{presetIdJson}},"programDigest":{{JsonSerializer.Serialize(programDigest)}},"rootPath":{{rootPathJson}},"rootSource":{{JsonSerializer.Serialize(rootSource)}},"sources":[{{sourceJson}}]}""";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalManifest))).ToLowerInvariant();
    }

    private static string NormalizePath (string path) => path.Replace('\\', '/');

    private static void AssertManifestSource (
        ProgramSourceManifestEntry source,
        string instancePath,
        string path,
        string documentDigest,
        int byteLength)
    {
        Assert.Equal(instancePath, source.InstancePath);
        Assert.Equal("request", source.Role);
        Assert.Equal(path, source.Path.Value);
        Assert.Equal(documentDigest, source.DocumentDigest.ToString());
        Assert.Equal(byteLength, source.ByteLength);
    }

    private sealed record ExpectedManifestSource (
        string InstancePath,
        string Path,
        string DocumentDigest,
        int ByteLength);

    private sealed class StubFileReader (Func<ContainedPath, ProgramDefinitionFileReadResult> read) : IProgramDefinitionFileReader
    {
        public List<ContainedPath> Paths { get; } = [];

        public ValueTask<ProgramDefinitionFileReadResult> ReadAsync (ContainedPath path, CancellationToken cancellationToken = default)
        {
            Paths.Add(path);
            return ValueTask.FromResult(read(path));
        }
    }
}
