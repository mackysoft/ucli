using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunArtifactValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateOutputPaths_WhenPathsAreAbsolute_ReturnsTrue ()
    {
        using var scope = TestDirectories.CreateTempScope("artifacts-validator", "paths-absolute");
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));

        var success = TestRunArtifactValidator.TryValidateOutputPaths(artifactPaths, out var errorMessage);

        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateOutputPaths_WhenPathsAreRelative_ReturnsFalse ()
    {
        var artifactPaths = new ArtifactPaths("run");

        var success = TestRunArtifactValidator.TryValidateOutputPaths(artifactPaths, out var errorMessage);

        Assert.False(success);
        Assert.Contains("path must be absolute", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateGeneratedFiles_WhenResultsAndEditorLogExist_ReturnsTrue ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifact-validator", "success");
        scope.WriteFile("run/results.xml", "<test-run />");
        scope.WriteFile("run/editor.log", "log");
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));

        var success = TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var errorMessage);

        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateGeneratedFiles_WhenResultsXmlDoesNotExist_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifact-validator", "missing-results");
        scope.WriteFile("run/editor.log", "log");
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));

        var success = TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var errorMessage);

        Assert.False(success);
        Assert.Contains("results.xml", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateGeneratedFiles_WhenEditorLogDoesNotExist_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifact-validator", "missing-editor-log");
        scope.WriteFile("run/results.xml", "<test-run />");
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));

        var success = TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var errorMessage);

        Assert.False(success);
        Assert.Contains("editor.log", errorMessage, StringComparison.Ordinal);
    }
}
