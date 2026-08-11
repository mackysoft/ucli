using System.Text;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Resolution;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Resolution;

public sealed class ProgramDefinitionResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WithReferencedRequest_RecordsStableManifestAndDefinitionDigest ()
    {
        const string program = """
        { "steps": [{ "command": "call", "requestPath": "requests/open.json" }] }
        """;
        const string request = """
        { "steps": [{ "kind": "op", "op": "ucli.scene.open", "args": { "path": "Assets/Main.unity" } }] }
        """;
        var root = Path.Combine(Path.GetTempPath(), $"ucli-program-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "requests"));
        File.WriteAllText(Path.Combine(root, "requests", "open.json"), request);
        var reader = new StubFileReader(
            new Dictionary<string, byte[]>(StringComparer.Ordinal),
            Encoding.UTF8.GetBytes(request));
        var resolver = new ProgramDefinitionResolver(new ProgramJsonParser(), reader);

        var first = await resolver.ResolveAsync(new ProgramDefinitionResolutionInput(program, ProgramRootSource.File, Path.Combine(root, "program.json"), null, root));
        var second = await resolver.ResolveAsync(new ProgramDefinitionResolutionInput(program, ProgramRootSource.File, Path.Combine(root, "program.json"), null, root));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Definition!.DefinitionDigest, second.Definition!.DefinitionDigest);
        var source = Assert.Single(first.Definition.Sources);
        Assert.Equal("/steps/0/requestPath", source.InstancePath);
        Assert.Equal("requests/open.json", source.Path);
        Assert.Matches("^[0-9a-f]{64}$", first.Definition.SourceManifest.Digest);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WithCanonicalDefinition_ProducesSpecificationFixedDigestsAndManifest ()
    {
        const string program = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"requests/open.json\"}]}";
        const string request = "{\"steps\":[{\"args\":{\"path\":\"Assets/Main.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        var result = await CreateResolver(request).ResolveAsync(new ProgramDefinitionResolutionInput(
            program,
            ProgramRootSource.File,
            "/program-root/program.json",
            PresetId: null,
            ReferenceRootPath: "/program-root"));

        var definition = Assert.IsType<ResolvedProgramDefinition>(result.Definition);
        Assert.Equal("8c68006091e3bacf1c7a2993d4b0304005559bf73eb807893a5aaa7cebfcec22", definition.DefinitionDigest);
        Assert.Equal("3de91402d7d59534368be1b06c68963855146f8669e6e10a802fa989eb7791c9", definition.SourceManifest.ProgramDigest);
        Assert.Equal("8f8f5e1c6b99d75819fa98c7c6db81774c07302978f48cf0ab1cd285e4c39d4a", definition.SourceManifest.Digest);
        Assert.Equal(ProgramRootSource.File, definition.SourceManifest.RootSource);
        Assert.Equal("/program-root/program.json", definition.SourceManifest.RootPath);
        Assert.Null(definition.SourceManifest.PresetId);
        var source = Assert.Single(definition.SourceManifest.Sources);
        Assert.Equal("/steps/0/requestPath", source.InstancePath);
        Assert.Equal("request", source.Role);
        Assert.Equal("requests/open.json", source.Path);
        Assert.Equal("e3df0c26993e2ef372cf23b712acf537ffc6ef036c3c46cb39007d17337db24a", source.DocumentDigest);
        Assert.Equal(84, source.ByteLength);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WithWhitespaceAndPropertyOrderOnlyDifferences_ProducesSameDefinitionDigest ()
    {
        const string canonicalProgram = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"requests/open.json\"}]}";
        const string equivalentProgram = """
        {
          "steps": [ { "requestPath": "requests/open.json", "command": "call" } ]
        }
        """;
        const string canonicalRequest = "{\"steps\":[{\"args\":{\"path\":\"Assets/Main.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        const string equivalentRequest = """
        { "steps": [ { "op": "ucli.scene.open", "kind": "op", "args": { "path": "Assets/Main.unity" } } ] }
        """;
        var canonical = await CreateResolver(canonicalRequest).ResolveAsync(CreateFileInput(canonicalProgram));
        var equivalent = await CreateResolver(equivalentRequest).ResolveAsync(CreateFileInput(equivalentProgram));

        Assert.True(canonical.IsSuccess);
        Assert.True(equivalent.IsSuccess);
        Assert.Equal(canonical.Definition!.DefinitionDigest, equivalent.Definition!.DefinitionDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WithMultipleRequestPaths_PreservesEveryStepReferenceInSourceManifestOrder ()
    {
        const string program = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"requests/a.json\"},{\"command\":\"call\",\"requestPath\":\"requests/b.json\"},{\"command\":\"call\",\"requestPath\":\"requests/a.json\"}]}";
        const string requestA = "{\"steps\":[{\"args\":{\"path\":\"Assets/A.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        const string requestB = "{\"steps\":[{\"args\":{\"path\":\"Assets/B.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        var resolver = new ProgramDefinitionResolver(
            new ProgramJsonParser(),
            new StubFileReader(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["/program-root/requests/a.json"] = Encoding.UTF8.GetBytes(requestA),
                ["/program-root/requests/b.json"] = Encoding.UTF8.GetBytes(requestB),
            }));

        var result = await resolver.ResolveAsync(CreateFileInput(program));

        var definition = Assert.IsType<ResolvedProgramDefinition>(result.Definition);
        Assert.Collection(
            definition.SourceManifest.Sources,
            source => AssertSource(source, "/steps/0/requestPath", "requests/a.json", "53f31283c94c6c2d88f8edd6cc02971ba147858044a3c17322d53e0432754fe4"),
            source => AssertSource(source, "/steps/1/requestPath", "requests/b.json", "7694887e4399cce340ace5349ddef42e9c90dd20a6879d5c47ba495551714819"),
            source => AssertSource(source, "/steps/2/requestPath", "requests/a.json", "53f31283c94c6c2d88f8edd6cc02971ba147858044a3c17322d53e0432754fe4"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WhenStepOrderReferencePathOrChildContentChanges_ChangesDefinitionDigest ()
    {
        const string request = "{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Main.unity\"}}]}";
        const string changedRequest = "{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Other.unity\"}}]}";
        var baseline = await CreateResolver(request).ResolveAsync(CreateFileInput("{\"steps\":[{\"command\":\"call\",\"requestPath\":\"requests/open.json\"}]}"));
        var changedPath = await CreateResolver(request, "requests/other.json").ResolveAsync(CreateFileInput("{\"steps\":[{\"command\":\"call\",\"requestPath\":\"requests/other.json\"}]}"));
        var changedChild = await CreateResolver(changedRequest).ResolveAsync(CreateFileInput("{\"steps\":[{\"command\":\"call\",\"requestPath\":\"requests/open.json\"}]}"));
        var firstOrder = await CreateResolver(request).ResolveAsync(CreateFileInput("{\"steps\":[{\"command\":\"ready\"},{\"command\":\"compile\"}]}"));
        var secondOrder = await CreateResolver(request).ResolveAsync(CreateFileInput("{\"steps\":[{\"command\":\"compile\"},{\"command\":\"ready\"}]}"));

        Assert.True(baseline.IsSuccess);
        Assert.True(changedPath.IsSuccess);
        Assert.True(changedChild.IsSuccess);
        Assert.True(firstOrder.IsSuccess);
        Assert.True(secondOrder.IsSuccess);
        Assert.NotEqual(baseline.Definition!.DefinitionDigest, changedPath.Definition!.DefinitionDigest);
        Assert.NotEqual(baseline.Definition!.DefinitionDigest, changedChild.Definition!.DefinitionDigest);
        Assert.NotEqual(firstOrder.Definition!.DefinitionDigest, secondOrder.Definition!.DefinitionDigest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WithRequestPathFromStdin_RejectsReferenceBoundary ()
    {
        const string program = """
        { "steps": [{ "command": "call", "requestPath": "request.json" }] }
        """;
        var resolver = new ProgramDefinitionResolver(
            new ProgramJsonParser(),
            new StubFileReader(new Dictionary<string, byte[]>(StringComparer.Ordinal)));

        var result = await resolver.ResolveAsync(new ProgramDefinitionResolutionInput(program, ProgramRootSource.Stdin, null, null, null));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("program.referenceBoundary", diagnostic.Code);
        Assert.Equal("/steps/0/requestPath", diagnostic.InstancePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WhenRequestPathAncestorSymlinkEscapesReferenceRoot_RejectsReferenceBoundary ()
    {
        const string program = """
        { "steps": [{ "command": "call", "requestPath": "requests/open.json" }] }
        """;
        var root = Path.Combine(Path.GetTempPath(), $"ucli-program-{Guid.NewGuid():N}");
        var externalRoot = Path.Combine(Path.GetTempPath(), $"ucli-program-external-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(externalRoot);
        File.WriteAllText(Path.Combine(externalRoot, "open.json"), "{ \"steps\": [] }");
        Directory.CreateSymbolicLink(Path.Combine(root, "requests"), externalRoot);
        try
        {
            var resolver = new ProgramDefinitionResolver(new ProgramJsonParser(), new StubFileReader(new Dictionary<string, byte[]>(StringComparer.Ordinal)));

            var result = await resolver.ResolveAsync(new ProgramDefinitionResolutionInput(program, ProgramRootSource.File, Path.Combine(root, "program.json"), null, root));

            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("program.referenceBoundary", diagnostic.Code);
            Assert.Equal("/steps/0/requestPath", diagnostic.InstancePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WhenRequestPathAncestorSymlinkStaysWithinReferenceRoot_AcceptsReference ()
    {
        const string program = """
        { "steps": [{ "command": "call", "requestPath": "requests/open.json" }] }
        """;
        const string request = """
        { "steps": [{ "kind": "op", "op": "ucli.scene.open", "args": { "path": "Assets/Main.unity" } }] }
        """;
        var root = Path.Combine(Path.GetTempPath(), $"ucli-program-{Guid.NewGuid():N}");
        var actualDirectory = Path.Combine(root, "actual");
        Directory.CreateDirectory(actualDirectory);
        File.WriteAllText(Path.Combine(actualDirectory, "open.json"), request);
        Directory.CreateSymbolicLink(Path.Combine(root, "requests"), actualDirectory);
        try
        {
            var resolver = new ProgramDefinitionResolver(
                new ProgramJsonParser(),
                new StubFileReader(new Dictionary<string, byte[]>(StringComparer.Ordinal), Encoding.UTF8.GetBytes(request)));

            var result = await resolver.ResolveAsync(new ProgramDefinitionResolutionInput(program, ProgramRootSource.File, Path.Combine(root, "program.json"), null, root));

            Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubFileReader (
        IReadOnlyDictionary<string, byte[]> files,
        byte[]? fallbackContent = null) : IProgramDefinitionFileReader
    {
        public ValueTask<ProgramDefinitionFileReadResult> ReadAsync (string path, CancellationToken cancellationToken = default)
        {
            return files.TryGetValue(path, out var content)
                ? ValueTask.FromResult(ProgramDefinitionFileReadResult.Success(content))
                : fallbackContent is not null
                    ? ValueTask.FromResult(ProgramDefinitionFileReadResult.Success(fallbackContent))
                    : ValueTask.FromResult(ProgramDefinitionFileReadResult.Failure("File does not exist."));
        }
    }

    private static ProgramDefinitionResolver CreateResolver (string request, string requestPath = "requests/open.json")
    {
        return new ProgramDefinitionResolver(
            new ProgramJsonParser(),
            new StubFileReader(new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [$"/program-root/{requestPath}"] = Encoding.UTF8.GetBytes(request),
            }));
    }

    private static ProgramDefinitionResolutionInput CreateFileInput (string json)
    {
        return new ProgramDefinitionResolutionInput(
            json,
            ProgramRootSource.File,
            "/program-root/program.json",
            PresetId: null,
            ReferenceRootPath: "/program-root");
    }

    private static void AssertSource (ProgramSourceManifestEntry source, string instancePath, string path, string documentDigest)
    {
        Assert.Equal(instancePath, source.InstancePath);
        Assert.Equal("request", source.Role);
        Assert.Equal(path, source.Path);
        Assert.Equal(documentDigest, source.DocumentDigest);
        Assert.Equal(81, source.ByteLength);
    }
}
