using MackySoft.Tests;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunProfileLoaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Load_WithValidProfile_ReturnsSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-profile-loader", "valid-profile");
        var profilePath = scope.WriteFile(
            "test.profile.json",
            """
            {
              "schemaVersion": 1,
              "projectPath": ".",
              "unityVersion": "6000.1.4f1",
              "testPlatform": "playmode",
              "buildTarget": "StandaloneWindows64",
              "timeoutSeconds": 90
            }
            """);
        var loader = new TestRunProfileLoader();

        var result = loader.Load(profilePath);

        Assert.True(result.IsSuccess);
        var profile = Assert.IsType<TestRunProfile>(result.Profile);
        Assert.Equal(TestRunProfile.SchemaVersionValue, profile.SchemaVersion);
        Assert.Equal(".", profile.ProjectPath);
        Assert.Equal("6000.1.4f1", profile.UnityVersion);
        Assert.Equal("playmode", profile.TestPlatform);
        Assert.Equal("StandaloneWindows64", profile.BuildTarget);
        Assert.Equal(90, profile.TimeoutSeconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Load_WithSchemaVersionMismatch_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-profile-loader", "schema-mismatch");
        var profilePath = scope.WriteFile(
            "test.profile.json",
            """
            {
              "schemaVersion": 9
            }
            """);
        var loader = new TestRunProfileLoader();

        var result = loader.Load(profilePath);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("schemaVersion", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Load_WithMissingProfilePath_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-profile-loader", "missing-profile");
        var loader = new TestRunProfileLoader();
        var missingPath = scope.GetPath("missing.profile.json");

        var result = loader.Load(missingPath);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("profilePath does not exist", error.Message, StringComparison.Ordinal);
    }
}