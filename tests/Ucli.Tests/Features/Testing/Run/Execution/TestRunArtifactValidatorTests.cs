using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunArtifactValidatorTests
{
    private static readonly MissingGeneratedFileCase[] MissingGeneratedFileCases =
    [
        new("missing-results", "run/editor.log", "results.xml"),
        new("missing-editor-log", "run/results.xml", "editor.log"),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateOutputPaths_WhenPathsAreAbsolute_ReturnsTrue ()
    {
        var artifactPaths = TestArtifactPaths.Create(Path.GetFullPath(Path.Combine("artifacts-validator", "run")));

        var success = TestRunArtifactValidator.TryValidateOutputPaths(artifactPaths, out var errorMessage);

        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateOutputPaths_WhenPathsAreRelative_ReturnsFalse ()
    {
        var artifactPaths = TestArtifactPaths.Create("run");

        var success = TestRunArtifactValidator.TryValidateOutputPaths(artifactPaths, out var errorMessage);

        Assert.False(success);
        Assert.Contains("path must be absolute", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void TryValidateGeneratedFiles_WhenResultsAndEditorLogExist_ReturnsTrue ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifact-validator", "success");
        scope.WriteFile("run/results.xml", "<test-run />");
        scope.WriteFile("run/editor.log", "log");
        var artifactPaths = TestArtifactPaths.Create(scope.GetPath("run"));

        var success = TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var errorMessage);

        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void TryValidateGeneratedFiles_WhenRequiredGeneratedFileDoesNotExist_ReturnsFalse ()
    {
        foreach (var testCase in MissingGeneratedFileCases)
        {
            using var scope = TestDirectories.CreateTempScope("test-run-artifact-validator", testCase.ScopeName);
            scope.WriteFile(testCase.ExistingRelativePath, "content");
            var artifactPaths = TestArtifactPaths.Create(scope.GetPath("run"));

            var success = TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var errorMessage);

            Assert.False(success);
            Assert.Contains(testCase.MissingFileName, errorMessage, StringComparison.Ordinal);
        }
    }

    private sealed record MissingGeneratedFileCase (
        string ScopeName,
        string ExistingRelativePath,
        string MissingFileName);
}
