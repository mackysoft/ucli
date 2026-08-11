using System.Text;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Presets;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Presets;

public sealed class ProgramPresetCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WhenProgramPathAncestorSymlinkEscapesUcliRoot_RejectsPresetPath ()
    {
        var ucliRoot = Path.Combine(Path.GetTempPath(), $"ucli-preset-{Guid.NewGuid():N}");
        var externalRoot = Path.Combine(Path.GetTempPath(), $"ucli-preset-external-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ucliRoot);
        Directory.CreateDirectory(externalRoot);
        File.WriteAllText(Path.Combine(externalRoot, "smoke.json"), "{ \"steps\": [{ \"command\": \"ready\" }] }");
        Directory.CreateSymbolicLink(Path.Combine(ucliRoot, "programs"), externalRoot);
        try
        {
            var config = UcliConfig.CreateDefault() with
            {
                ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
                {
                    ["smoke"] = new ProgramPresetRegistration("Runs smoke checks.", "programs/smoke.json"),
                },
            };
            var catalog = new ProgramPresetCatalog(
                new StubFileReader(),
                new ProgramDefinitionResolver(new ProgramJsonParser(), new StubFileReader()));

            var result = await catalog.ResolveAsync("smoke", config, ucliRoot);

            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("program.presetPathInvalid", diagnostic.Code);
        }
        finally
        {
            Directory.Delete(ucliRoot, recursive: true);
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveAsync_WhenProgramPathAncestorSymlinkStaysWithinUcliRoot_AcceptsPreset ()
    {
        var ucliRoot = Path.Combine(Path.GetTempPath(), $"ucli-preset-{Guid.NewGuid():N}");
        var actualDirectory = Path.Combine(ucliRoot, "actual");
        Directory.CreateDirectory(actualDirectory);
        File.WriteAllText(Path.Combine(actualDirectory, "smoke.json"), "{ \"steps\": [{ \"command\": \"ready\" }] }");
        Directory.CreateSymbolicLink(Path.Combine(ucliRoot, "programs"), actualDirectory);
        try
        {
            var config = UcliConfig.CreateDefault() with
            {
                ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
                {
                    ["smoke"] = new ProgramPresetRegistration("Runs smoke checks.", "programs/smoke.json"),
                },
            };
            var catalog = new ProgramPresetCatalog(
                new StubFileReader(),
                new ProgramDefinitionResolver(new ProgramJsonParser(), new StubFileReader()));

            var result = await catalog.ResolveAsync("smoke", config, ucliRoot);

            Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        }
        finally
        {
            Directory.Delete(ucliRoot, recursive: true);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_ResolvesEveryPresetThroughNormalDefinitionResolutionInOrdinalOrder ()
    {
        var configDirectory = CreateTestRoot();
        var reader = new RecordingFileReader(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Path.Combine(configDirectory, "alpha.json")] = "{ \"steps\": [{ \"command\": \"ready\" }] }",
            [Path.Combine(configDirectory, "zeta.json")] = "{ \"steps\": [{ \"command\": \"compile\" }] }",
        });
        var catalog = new ProgramPresetCatalog(reader, new ProgramDefinitionResolver(new ProgramJsonParser(), reader));
        var config = CreateConfig(
            new KeyValuePair<string, ProgramPresetRegistration>("zeta", new ProgramPresetRegistration("Compiles.", "zeta.json")),
            new KeyValuePair<string, ProgramPresetRegistration>("alpha", new ProgramPresetRegistration("Checks readiness.", "alpha.json")));

        var result = await catalog.ListAsync(config, configDirectory);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Presets);
        var presets = result.Presets!;
        Assert.Equal(["alpha", "zeta"], presets.Select(static preset => preset.Id));
        Assert.All(presets, static preset => Assert.Matches("^[0-9a-f]{64}$", preset.Definition.DefinitionDigest));
        Assert.Equal([Path.Combine(configDirectory, "alpha.json"), Path.Combine(configDirectory, "zeta.json")], reader.ReadPaths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ListAsync_WhenAnyPresetCannotResolve_ReturnsNoPartialList ()
    {
        var configDirectory = CreateTestRoot();
        var reader = new RecordingFileReader(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Path.Combine(configDirectory, "alpha.json")] = "{ \"steps\": [{ \"command\": \"ready\" }] }",
            [Path.Combine(configDirectory, "zeta.json")] = "{}",
        });
        var catalog = new ProgramPresetCatalog(reader, new ProgramDefinitionResolver(new ProgramJsonParser(), reader));
        var config = CreateConfig(
            new KeyValuePair<string, ProgramPresetRegistration>("alpha", new ProgramPresetRegistration("Checks readiness.", "alpha.json")),
            new KeyValuePair<string, ProgramPresetRegistration>("zeta", new ProgramPresetRegistration("Compiles.", "zeta.json")));

        var result = await catalog.ListAsync(config, configDirectory);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Presets);
        Assert.NotEmpty(result.Diagnostics);
    }

    private sealed class StubFileReader : IProgramDefinitionFileReader
    {
        public ValueTask<ProgramDefinitionFileReadResult> ReadAsync (string path, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ProgramDefinitionFileReadResult.Success(Encoding.UTF8.GetBytes("{ \"steps\": [{ \"command\": \"ready\" }] }")));
        }
    }

    private sealed class RecordingFileReader (IReadOnlyDictionary<string, string> documents) : IProgramDefinitionFileReader
    {
        public List<string> ReadPaths { get; } = [];

        public ValueTask<ProgramDefinitionFileReadResult> ReadAsync (string path, CancellationToken cancellationToken = default)
        {
            ReadPaths.Add(path);
            return documents.TryGetValue(path, out var document)
                ? ValueTask.FromResult(ProgramDefinitionFileReadResult.Success(Encoding.UTF8.GetBytes(document)))
                : ValueTask.FromResult(ProgramDefinitionFileReadResult.Failure("File does not exist."));
        }
    }

    private static UcliConfig CreateConfig (params KeyValuePair<string, ProgramPresetRegistration>[] entries)
    {
        return UcliConfig.CreateDefault() with
        {
            ProgramPresets = new Dictionary<string, ProgramPresetRegistration>(entries, StringComparer.Ordinal),
        };
    }

    private static string CreateTestRoot () => Path.GetFullPath($"ucli-program-preset-catalog-tests-{Guid.NewGuid():N}");
}
