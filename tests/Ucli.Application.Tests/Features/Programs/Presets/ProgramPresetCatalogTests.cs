using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Presets;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Presets;

public sealed class ProgramPresetCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_UsesTheContainedReaderForPresetAndItsPhysicalParentForReferences ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-preset-{Guid.NewGuid():N}"));
        var physicalDirectory = AbsolutePath.Resolve(root, "actual");
        var reader = new StubFileReader(path =>
        {
            if (path.RelativePath.Value == "programs/smoke.json")
            {
                return new ProgramDefinitionFileReadSuccess(
                    Encoding.UTF8.GetBytes("{\"steps\":[{\"command\":\"call\",\"requestPath\":\"request.json\"}]}"),
                    AbsolutePath.Resolve(physicalDirectory, "smoke.json"));
            }

            Assert.Equal(physicalDirectory.Value, path.BoundaryRoot.Value);
            return new ProgramDefinitionFileReadSuccess(
                Encoding.UTF8.GetBytes("{\"steps\":[{\"kind\":\"op\",\"op\":\"ucli.scene.open\",\"args\":{\"path\":\"Assets/Main.unity\"}}]}"),
                AbsolutePath.Resolve(physicalDirectory, "request.json"));
        });
        var catalog = new ProgramPresetCatalog(reader, new ProgramDefinitionResolver(new ProgramJsonParser(), reader));
        var config = UcliConfig.CreateDefault() with
        {
            ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
            {
                ["smoke"] = new("Runs smoke checks.", RootRelativePath.Parse("programs/smoke.json")),
            },
        };

        var result = await catalog.ResolveAsync("smoke", config, root.Value);

        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(static value => value.Message)));
        var definition = Assert.IsType<ProgramPresetResolution>(result.Preset).Definition;
        Assert.Equal(ProgramRootSource.Preset, definition.SourceManifest.RootSource);
        Assert.Null(definition.SourceManifest.RootPath);
        Assert.Equal("smoke", definition.SourceManifest.PresetId);
        var source = Assert.Single(definition.SourceManifest.Sources);
        Assert.Equal("/steps/0/requestPath", source.InstancePath);
        Assert.Equal("request", source.Role);
        Assert.Equal("request.json", source.Path.Value);
        Assert.Equal("e3df0c26993e2ef372cf23b712acf537ffc6ef036c3c46cb39007d17337db24a", source.DocumentDigest.ToString());
        Assert.Equal(84, source.ByteLength);
        Assert.Equal(
            ComputeManifestDigest(
                presetId: "smoke",
                programDigest: "99984431d5522d67f1f294756ddf3ee15740de405f535bea836414f6e9c31093",
                instancePath: "/steps/0/requestPath",
                path: "request.json",
                documentDigest: "e3df0c26993e2ef372cf23b712acf537ffc6ef036c3c46cb39007d17337db24a",
                byteLength: 84),
            definition.SourceManifest.Digest.ToString());
        Assert.Equal(["programs/smoke.json", "request.json"], reader.Paths.Select(static value => value.RelativePath.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_OutsidePresetRead_IsPresetPathInvalid ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-preset-{Guid.NewGuid():N}"));
        var reader = new StubFileReader(_ => new ProgramDefinitionFileReadOutsideBoundary());
        var catalog = new ProgramPresetCatalog(reader, new ProgramDefinitionResolver(new ProgramJsonParser(), reader));
        var config = UcliConfig.CreateDefault() with
        {
            ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
            {
                ["smoke"] = new("Runs smoke checks.", RootRelativePath.Parse("programs/smoke.json")),
            },
        };

        var result = await catalog.ResolveAsync("smoke", config, root.Value);

        Assert.Equal("program.presetPathInvalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_InvalidUtf8Preset_StopsBeforeDefinitionResolution ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-preset-{Guid.NewGuid():N}"));
        var reader = new StubFileReader(_ => new ProgramDefinitionFileReadSuccess([0xff], AbsolutePath.Resolve(root, "programs/smoke.json")));
        var catalog = new ProgramPresetCatalog(reader, new ProgramDefinitionResolver(new ProgramJsonParser(), reader));
        var config = UcliConfig.CreateDefault() with
        {
            ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
            {
                ["smoke"] = new("Runs smoke checks.", RootRelativePath.Parse("programs/smoke.json")),
            },
        };

        var result = await catalog.ResolveAsync("smoke", config, root.Value);

        Assert.Equal("program.presetReadFailed", Assert.Single(result.Diagnostics).Code);
        Assert.Single(reader.Paths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_ResolvesEveryPresetInOrdinalOrderThroughEachReceiptAndItsReferences ()
    {
        const string alphaProgram = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"alpha-request.json\"}]}";
        const string zetaProgram = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"zeta-request.json\"}]}";
        const string alphaRequest = "{\"steps\":[{\"args\":{\"path\":\"Assets/Alpha.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        const string zetaRequest = "{\"steps\":[{\"args\":{\"path\":\"Assets/Zeta.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-preset-{Guid.NewGuid():N}"));
        var reader = new StubFileReader(path => path.RelativePath.Value switch
        {
            "alpha.json" => new ProgramDefinitionFileReadSuccess(Encoding.UTF8.GetBytes(alphaProgram), AbsolutePath.Resolve(root, "alpha.json")),
            "alpha-request.json" => new ProgramDefinitionFileReadSuccess(Encoding.UTF8.GetBytes(alphaRequest), AbsolutePath.Resolve(root, "alpha-request.json")),
            "zeta.json" => new ProgramDefinitionFileReadSuccess(Encoding.UTF8.GetBytes(zetaProgram), AbsolutePath.Resolve(root, "zeta.json")),
            "zeta-request.json" => new ProgramDefinitionFileReadSuccess(Encoding.UTF8.GetBytes(zetaRequest), AbsolutePath.Resolve(root, "zeta-request.json")),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected path: {path.RelativePath.Value}"),
        });
        var catalog = new ProgramPresetCatalog(reader, new ProgramDefinitionResolver(new ProgramJsonParser(), reader));
        var config = UcliConfig.CreateDefault() with
        {
            ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
            {
                ["zeta"] = new("Compiles.", RootRelativePath.Parse("zeta.json")),
                ["alpha"] = new("Checks readiness.", RootRelativePath.Parse("alpha.json")),
            },
        };

        var result = await catalog.ListAsync(config, root.Value);

        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(static value => value.Message)));
        Assert.NotNull(result.Presets);
        Assert.Equal(["alpha", "zeta"], result.Presets.Select(static preset => preset.Id));
        Assert.Equal(
            ["alpha.json", "alpha-request.json", "zeta.json", "zeta-request.json"],
            reader.Paths.Select(static path => path.RelativePath.Value));
        Assert.All(reader.Paths, path => Assert.Equal(root, path.BoundaryRoot));
        Assert.Equal(1, reader.Paths.Count(path => path.RelativePath.Value == "alpha.json"));
        Assert.Equal(1, reader.Paths.Count(path => path.RelativePath.Value == "zeta.json"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_WhenAnIntermediatePresetCannotBeRead_ReturnsNoPartialListAndDoesNotReadLaterPresets ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-preset-{Guid.NewGuid():N}"));
        var reader = new StubFileReader(path => path.RelativePath.Value switch
        {
            "alpha.json" => new ProgramDefinitionFileReadSuccess(Encoding.UTF8.GetBytes("{\"steps\":[{\"command\":\"ready\"}]}"), AbsolutePath.Resolve(root, "alpha.json")),
            "beta.json" => new ProgramDefinitionFileReadUnavailable("beta.json cannot be read safely."),
            "zeta.json" => throw new Xunit.Sdk.XunitException("Later preset must not be read."),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected path: {path.RelativePath.Value}"),
        });
        var catalog = new ProgramPresetCatalog(reader, new ProgramDefinitionResolver(new ProgramJsonParser(), reader));
        var config = UcliConfig.CreateDefault() with
        {
            ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
            {
                ["zeta"] = new("Compiles.", RootRelativePath.Parse("zeta.json")),
                ["beta"] = new("Fails safely.", RootRelativePath.Parse("beta.json")),
                ["alpha"] = new("Checks readiness.", RootRelativePath.Parse("alpha.json")),
            },
        };

        var result = await catalog.ListAsync(config, root.Value);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Presets);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("program.presetReadFailed", diagnostic.Code);
        Assert.Equal("beta.json cannot be read safely.", diagnostic.Message);
        Assert.Equal(["alpha.json", "beta.json"], reader.Paths.Select(static path => path.RelativePath.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_NonAsciiRequest_UsesUtf8ByteLengthAndFixedCanonicalManifestDigest ()
    {
        const string program = "{\"steps\":[{\"command\":\"call\",\"requestPath\":\"要求.json\"}]}";
        const string request = "{\"steps\":[{\"args\":{\"path\":\"Assets/シーン.unity\"},\"kind\":\"op\",\"op\":\"ucli.scene.open\"}]}";
        const string expectedProgramDigest = "b4effdbc4b2a713620c8a06983b64f7f25cf236afb6d03d3f069fc3d4a598a8c";
        const string expectedDocumentDigest = "c62c1fdf80ccd9f0cf629e490ae7400875d40bed4e9554e57e81d4399fd09296";
        const string expectedManifestDigest = "a7bece1eda229b23c9acf5d2f20dc0e2cb4940726c2e8bd346c314a78d9f1e74";
        const string canonicalManifest = "{\"presetId\":\"unicode\",\"programDigest\":\"b4effdbc4b2a713620c8a06983b64f7f25cf236afb6d03d3f069fc3d4a598a8c\",\"rootPath\":null,\"rootSource\":\"preset\",\"sources\":[{\"byteLength\":89,\"documentDigest\":\"c62c1fdf80ccd9f0cf629e490ae7400875d40bed4e9554e57e81d4399fd09296\",\"instancePath\":\"/steps/0/requestPath\",\"path\":\"要求.json\",\"role\":\"request\"}]}";
        var root = AbsolutePath.Parse(Path.GetFullPath($"ucli-program-preset-{Guid.NewGuid():N}"));
        var physicalDirectory = AbsolutePath.Resolve(root, "actual");
        var reader = new StubFileReader(path => path.RelativePath.Value == "programs/unicode.json"
            ? new ProgramDefinitionFileReadSuccess(Encoding.UTF8.GetBytes(program), AbsolutePath.Resolve(physicalDirectory, "unicode.json"))
            : new ProgramDefinitionFileReadSuccess(Encoding.UTF8.GetBytes(request), AbsolutePath.Resolve(physicalDirectory, "要求.json")));
        var catalog = new ProgramPresetCatalog(reader, new ProgramDefinitionResolver(new ProgramJsonParser(), reader));
        var config = UcliConfig.CreateDefault() with
        {
            ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
            {
                ["unicode"] = new("Unicode request.", RootRelativePath.Parse("programs/unicode.json")),
            },
        };

        var result = await catalog.ResolveAsync("unicode", config, root.Value);

        var definition = Assert.IsType<ProgramPresetResolution>(result.Preset).Definition;
        var source = Assert.Single(definition.SourceManifest.Sources);
        Assert.Equal("\"\\u8981\\u6C42.json\"", JsonSerializer.Serialize("要求.json"));
        Assert.Equal("/steps/0/requestPath", source.InstancePath);
        Assert.Equal("request", source.Role);
        Assert.Equal("要求.json", source.Path.Value);
        Assert.Equal(Encoding.UTF8.GetByteCount(request), source.ByteLength);
        Assert.Equal(89, source.ByteLength);
        Assert.Equal(expectedDocumentDigest, source.DocumentDigest.ToString());
        Assert.Equal(expectedProgramDigest, definition.SourceManifest.ProgramDigest.ToString());
        Assert.Equal(expectedManifestDigest, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalManifest))).ToLowerInvariant());
        Assert.Equal(expectedManifestDigest, definition.SourceManifest.Digest.ToString());
    }

    private sealed class StubFileReader (Func<ContainedPath, ProgramDefinitionFileReadResult> read) : IProgramDefinitionFileReader
    {
        public List<ContainedPath> Paths { get; } = [];

        public ValueTask<ProgramDefinitionFileReadResult> ReadAsync (ContainedPath path, CancellationToken cancellationToken = default)
        {
            Paths.Add(path);
            return ValueTask.FromResult(read(path));
        }
    }

    private static string ComputeManifestDigest (
        string presetId,
        string programDigest,
        string instancePath,
        string path,
        string documentDigest,
        int byteLength)
    {
        // The property order is the RFC 8785 order of this closed manifest shape.
        var canonicalManifest = $$"""{"presetId":{{JsonSerializer.Serialize(presetId)}},"programDigest":{{JsonSerializer.Serialize(programDigest)}},"rootPath":null,"rootSource":{{JsonSerializer.Serialize("preset")}},"sources":[{"byteLength":{{byteLength}},"documentDigest":{{JsonSerializer.Serialize(documentDigest)}},"instancePath":{{JsonSerializer.Serialize(instancePath)}},"path":{{JsonSerializer.Serialize(path)}},"role":{{JsonSerializer.Serialize("request")}}}]}""";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalManifest))).ToLowerInvariant();
    }
}
