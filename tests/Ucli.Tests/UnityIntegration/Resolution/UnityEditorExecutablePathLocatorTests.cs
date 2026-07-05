namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class UnityEditorExecutablePathLocatorTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithoutPreferredPath_UsesSearchRoots ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-executable-path-locator", "search-root-success");
        var searchRootPath = scope.CreateDirectory("SearchRoot");
        var executablePath = UnityEditorInstallationTestFactory.WriteEditorExecutable(scope, "SearchRoot", "6000.1.4f1");

        var result = UnityEditorExecutablePathLocator.Resolve("6000.1.4f1", null, new[] { searchRootPath });

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(executablePath), result.UnityEditorPath);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithoutPreferredPath_WhenInstallationMissing_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-executable-path-locator", "search-root-missing");
        var searchRootPath = scope.CreateDirectory("SearchRoot");

        var result = UnityEditorExecutablePathLocator.Resolve("6000.1.4f1", null, new[] { searchRootPath });

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("not installed", error.Message, StringComparison.Ordinal);
    }

}
