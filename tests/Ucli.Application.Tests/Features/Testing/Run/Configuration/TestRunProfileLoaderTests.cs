using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunProfileLoaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithValidProfile_ReturnsSuccess ()
    {
        var loader = CreateLoader(
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": "6000.1.4f1",
              "unityEditorPath": null,
              "testPlatform": "StandaloneWindows64",
              "testFilter": null,
              "testCategories": ["smoke"],
              "assemblyNames": ["Game.Tests"],
              "testSettingsPath": null,
              "timeout": 90
            }
            """);

        var result = await loader.LoadAsync("test.profile.json", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var profile = Assert.IsType<TestRunProfile>(result.Profile);
        Assert.Equal(TestRunProfile.SchemaVersionValue, profile.SchemaVersion);
        Assert.Equal(".", profile.ProjectPath);
        Assert.Equal("6000.1.4f1", profile.UnityVersion);
        Assert.Equal("StandaloneWindows64", profile.TestPlatform);
        Assert.Equal(90, profile.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithCommaSeparatedListEntries_SplitsAndDeduplicatesValues ()
    {
        var loader = CreateLoader(
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": null,
              "unityEditorPath": null,
              "testPlatform": "editmode",
              "testFilter": null,
              "testCategories": ["smoke, quick", "smoke", "nightly"],
              "assemblyNames": ["Game.Tests, Game.MoreTests", "Game.Tests"],
              "testSettingsPath": null,
              "timeout": 90
            }
            """);

        var result = await loader.LoadAsync("test.profile.json", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var profile = Assert.IsType<TestRunProfile>(result.Profile);
        Assert.NotNull(profile.TestCategories);
        Assert.NotNull(profile.AssemblyNames);
        Assert.Equal(["smoke", "quick", "nightly"], profile.TestCategories);
        Assert.Equal(["Game.Tests", "Game.MoreTests"], profile.AssemblyNames);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithSchemaVersionMismatch_ReturnsInvalidArgument ()
    {
        var loader = CreateLoader(
            """
            {
              "schemaVersion": 9,
              "projectPath": ".",
              "unityVersion": null,
              "unityEditorPath": null,
              "testPlatform": "editmode",
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "testSettingsPath": null,
              "timeout": 90
            }
            """);

        var result = await loader.LoadAsync("test.profile.json", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("schemaVersion", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithLegacyBuildTargetProperty_ReturnsInvalidArgument ()
    {
        var loader = CreateLoader(
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": null,
              "unityEditorPath": null,
              "testPlatform": "playmode",
              "buildTarget": "Android",
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "testSettingsPath": null,
              "timeout": 90
            }
            """);

        var result = await loader.LoadAsync("test.profile.json", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("unknown property", error.Message, StringComparison.Ordinal);
        Assert.Contains("buildTarget", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithLegacyTimeoutProperty_ReturnsInvalidArgument ()
    {
        var loader = CreateLoader(
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": null,
              "unityEditorPath": null,
              "testPlatform": "editmode",
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "testSettingsPath": null,
              "timeoutSeconds": 90
            }
            """);

        var result = await loader.LoadAsync("test.profile.json", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("unknown property", error.Message, StringComparison.Ordinal);
        Assert.Contains("timeoutSeconds", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithNonPositiveTimeout_ReturnsInvalidArgument ()
    {
        var loader = CreateLoader(
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": null,
              "unityEditorPath": null,
              "testPlatform": "editmode",
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "testSettingsPath": null,
              "timeout": 0
            }
            """);

        var result = await loader.LoadAsync("test.profile.json", CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
    }

    private static TestRunProfileLoader CreateLoader (string json)
    {
        return new TestRunProfileLoader(new StubProfileJsonReader(json));
    }

    private sealed class StubProfileJsonReader : ITestRunProfileJsonReader
    {
        private readonly string json;

        public StubProfileJsonReader (string json)
        {
            this.json = json;
        }

        public ValueTask<TestRunProfileJsonReadResult> ReadTextAsync (
            string profilePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(TestRunProfileJsonReadResult.Success(json));
        }
    }
}
