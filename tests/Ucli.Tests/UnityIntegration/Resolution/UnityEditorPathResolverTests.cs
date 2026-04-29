namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class UnityEditorPathResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithPreferredExecutablePath_ReturnsNormalizedPath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "preferred-file");
        var executablePath = EnsureEditorInstallation(scope, "Editors", "6000.1.4f1");
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(Array.Empty<string>()));

        var result = resolver.Resolve("6000.1.4f1", executablePath);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(executablePath), result.UnityEditorPath);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithPreferredDirectoryPath_ReturnsExecutablePath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "preferred-directory");
        var executablePath = EnsureEditorInstallation(scope, "Editors", "6000.1.4f1");
        var versionDirectoryPath = Path.GetDirectoryName(Path.GetDirectoryName(executablePath)!)!;
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(Array.Empty<string>()));

        var result = resolver.Resolve("6000.1.4f1", versionDirectoryPath);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(executablePath), result.UnityEditorPath);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenPreferredPathDoesNotExist_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "missing-preferred-path");
        var missingPath = scope.GetPath(Path.Combine("Missing", "Editor", "Unity.exe"));
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(Array.Empty<string>()));

        var result = resolver.Resolve("6000.1.4f1", missingPath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("unityEditorPath does not exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenPreferredPathHasUnsupportedExecutableName_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "unsupported-executable");
        var filePath = scope.WriteFile(Path.Combine("Editors", "not-unity-binary"), string.Empty);
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(Array.Empty<string>()));

        var result = resolver.Resolve("6000.1.4f1", filePath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("must point to a Unity executable", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenPreferredPathVersionMismatches_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "version-mismatch");
        var executablePath = EnsureEditorInstallation(scope, "Editors", "6000.1.3f1");
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(Array.Empty<string>()));

        var result = resolver.Resolve("6000.1.4f1", executablePath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("conflicts", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenUnityVersionHasCSuffix_AllowsPreferredPath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "c-suffix");
        const string unityVersion = "2022.3.5f1c1";
        var executablePath = EnsureEditorInstallation(scope, "Editors", unityVersion);
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(Array.Empty<string>()));

        var result = resolver.Resolve(unityVersion, executablePath);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(executablePath), result.UnityEditorPath);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithoutPreferredPath_UsesSearchRoots ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "search-root-success");
        var searchRootPath = scope.CreateDirectory("SearchRoot");
        var executablePath = EnsureEditorInstallation(scope, "SearchRoot", "6000.1.4f1");
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(searchRootPath));

        var result = resolver.Resolve("6000.1.4f1", null);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(executablePath), result.UnityEditorPath);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithoutPreferredPath_WhenInstallationMissing_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "search-root-missing");
        var searchRootPath = scope.CreateDirectory("SearchRoot");
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(searchRootPath));

        var result = resolver.Resolve("6000.1.4f1", null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("not installed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenUnityVersionIsWhitespace_ReturnsInvalidArgumentError ()
    {
        var resolver = new UnityEditorPathResolver(new StubSearchRootProvider(Array.Empty<string>()));

        var result = resolver.Resolve(" ", null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("Unity version must not be", error.Message, StringComparison.Ordinal);
    }

    private static string EnsureEditorInstallation (
        TestDirectoryScope scope,
        string rootRelativePath,
        string unityVersion)
    {
        var versionRelativePath = Path.Combine(rootRelativePath, unityVersion);
        return scope.WriteFile(Path.Combine(versionRelativePath, "Editor", "Unity.exe"), string.Empty);
    }

    private sealed class StubSearchRootProvider : IUnityEditorSearchRootProvider
    {
        private readonly IReadOnlyList<string> searchRoots;

        public StubSearchRootProvider (params string[] searchRoots)
        {
            this.searchRoots = searchRoots;
        }

        public IReadOnlyList<string> GetSearchRoots ()
        {
            return searchRoots;
        }
    }
}
