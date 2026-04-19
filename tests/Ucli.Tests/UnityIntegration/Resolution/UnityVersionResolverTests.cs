namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class UnityVersionResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithPreferredUnityVersion_ReturnsPreferredValue ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-version-resolver", "preferred-version");
        var projectPath = UnityProjectTestFactory.CreateMinimalUnityProject(
            scope,
            "UnityProject",
            "m_EditorVersion: 6000.1.4f1");
        var resolver = new UnityVersionResolver();

        var result = resolver.Resolve(projectPath, "6000.1.8f1");

        Assert.True(result.IsSuccess);
        Assert.Equal("6000.1.8f1", result.UnityVersion);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithoutPreferredUnityVersion_ReadsProjectVersionFile ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-version-resolver", "project-version-fallback");
        var projectPath = UnityProjectTestFactory.CreateMinimalUnityProject(
            scope,
            "UnityProject",
            "m_EditorVersion: 2022.3.5f1");
        var resolver = new UnityVersionResolver();

        var result = resolver.Resolve(projectPath, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("2022.3.5f1", result.UnityVersion);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenProjectVersionFileMissing_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-version-resolver", "missing-project-version");
        var projectPath = scope.CreateDirectory("UnityProject");
        var resolver = new UnityVersionResolver();

        var result = resolver.Resolve(projectPath, null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityVersion);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ProjectVersion.txt does not exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenProjectVersionContentIsMalformed_ReturnsInvalidArgumentError ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-version-resolver", "malformed-project-version");
        var projectPath = UnityProjectTestFactory.CreateMinimalUnityProject(
            scope,
            "UnityProject",
            "m_EditorVersionWithRevision: 6000.1.4f1");
        var resolver = new UnityVersionResolver();

        var result = resolver.Resolve(projectPath, null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityVersion);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("m_EditorVersion is missing or invalid", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Resolve_WhenProjectPathIsInvalid_ReturnsInvalidArgumentError (string? projectPath)
    {
        var resolver = new UnityVersionResolver();

        var result = resolver.Resolve(projectPath!, null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.UnityVersion);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("Unity project path must not be", error.Message, StringComparison.Ordinal);
    }
}