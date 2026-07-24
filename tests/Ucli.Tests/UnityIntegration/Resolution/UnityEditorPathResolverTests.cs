namespace MackySoft.Ucli.Tests;

using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class UnityEditorPathResolverTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithPreferredExecutablePath_ReturnsNormalizedPath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "preferred-file");
        var executablePath = UnityEditorInstallationTestFactory.WriteEditorExecutable(scope, "Editors", "6000.1.4f1");
        var resolver = CreateResolver();

        var result = resolver.Resolve("6000.1.4f1", executablePath);

        Assert.True(result.IsSuccess);
        Assert.Equal(AbsolutePath.Parse(executablePath), result.UnityEditorPath);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithPreferredDirectoryPath_ReturnsExecutablePath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "preferred-directory");
        var executablePath = UnityEditorInstallationTestFactory.WriteEditorExecutable(scope, "Editors", "6000.1.4f1");
        var versionDirectoryPath = Path.GetDirectoryName(Path.GetDirectoryName(executablePath)!)!;
        var resolver = CreateResolver();

        var result = resolver.Resolve("6000.1.4f1", versionDirectoryPath);

        Assert.True(result.IsSuccess);
        Assert.Equal(AbsolutePath.Parse(executablePath), result.UnityEditorPath);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WhenPreferredPathDoesNotExist_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "missing-preferred-path");
        var missingPath = scope.GetPath(Path.Combine("Missing", "Editor", "Unity.exe"));
        var resolver = CreateResolver();

        var result = resolver.Resolve("6000.1.4f1", missingPath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("unityEditorPath does not exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WhenPreferredPathHasUnsupportedExecutableName_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "unsupported-executable");
        var filePath = scope.WriteFile(Path.Combine("Editors", "not-unity-binary"), string.Empty);
        var resolver = CreateResolver();

        var result = resolver.Resolve("6000.1.4f1", filePath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("must point to a Unity executable", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WhenPreferredPathVersionMismatches_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "version-mismatch");
        var executablePath = UnityEditorInstallationTestFactory.WriteEditorExecutable(scope, "Editors", "6000.1.3f1");
        var resolver = CreateResolver();

        var result = resolver.Resolve("6000.1.4f1", executablePath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("conflicts", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WhenUnityVersionHasCSuffix_AllowsPreferredPath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-editor-path-resolver", "c-suffix");
        const string unityVersion = "2022.3.5f1c1";
        var executablePath = UnityEditorInstallationTestFactory.WriteEditorExecutable(scope, "Editors", unityVersion);
        var resolver = CreateResolver();

        var result = resolver.Resolve(unityVersion, executablePath);

        Assert.True(result.IsSuccess);
        Assert.Equal(AbsolutePath.Parse(executablePath), result.UnityEditorPath);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenUnityVersionIsWhitespace_ReturnsInvalidArgumentError ()
    {
        var resolver = CreateResolver();

        var result = resolver.Resolve(" ", null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityEditorPath);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("Unity version must not be", error.Message, StringComparison.Ordinal);
    }

    private static UnityEditorPathResolver CreateResolver ()
    {
        return new UnityEditorPathResolver();
    }
}
