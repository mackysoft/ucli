using MackySoft.Tests;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunProfileLoaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithValidProfile_ReturnsSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-profile-loader", "valid-profile");
        var profilePath = scope.WriteFile(
            "test.profile.json",
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": "6000.1.4f1",
              "unityEditorPath": null,
              "testPlatform": "playmode",
              "buildTarget": "StandaloneWindows64",
              "testFilter": null,
              "testCategories": ["smoke"],
              "assemblyNames": ["Game.Tests"],
              "testSettingsPath": null,
              "timeout": 90
            }
            """);
        var loader = new TestRunProfileLoader();

        var result = await loader.Load(profilePath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var profile = Assert.IsType<TestRunProfile>(result.Profile);
        Assert.Equal(TestRunProfile.SchemaVersionValue, profile.SchemaVersion);
        Assert.Equal(".", profile.ProjectPath);
        Assert.Equal("6000.1.4f1", profile.UnityVersion);
        Assert.Equal("playmode", profile.TestPlatform);
        Assert.Equal("StandaloneWindows64", profile.BuildTarget);
        Assert.Equal(90, profile.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithSchemaVersionMismatch_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-profile-loader", "schema-mismatch");
        var profilePath = scope.WriteFile(
            "test.profile.json",
            """
            {
              "schemaVersion": 9,
              "projectPath": ".",
              "unityVersion": null,
              "unityEditorPath": null,
              "testPlatform": "editmode",
              "buildTarget": null,
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "testSettingsPath": null,
              "timeout": 90
            }
            """);
        var loader = new TestRunProfileLoader();

        var result = await loader.Load(profilePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("schemaVersion", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithLegacyTimeoutProperty_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-profile-loader", "legacy-timeout-property");
        var profilePath = scope.WriteFile(
            "test.profile.json",
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": null,
              "unityEditorPath": null,
              "testPlatform": "editmode",
              "buildTarget": null,
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "testSettingsPath": null,
              "timeoutSeconds": 90
            }
            """);
        var loader = new TestRunProfileLoader();

        var result = await loader.Load(profilePath, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("test-run-profile-loader", "non-positive-timeout");
        var profilePath = scope.WriteFile(
            "test.profile.json",
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": null,
              "unityEditorPath": null,
              "testPlatform": "editmode",
              "buildTarget": null,
              "testFilter": null,
              "testCategories": [],
              "assemblyNames": [],
              "testSettingsPath": null,
              "timeout": 0
            }
            """);
        var loader = new TestRunProfileLoader();

        var result = await loader.Load(profilePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithMissingProfilePath_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-profile-loader", "missing-profile");
        var loader = new TestRunProfileLoader();
        var missingPath = scope.GetPath("missing.profile.json");

        var result = await loader.Load(missingPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("profilePath does not exist", error.Message, StringComparison.Ordinal);
    }
}