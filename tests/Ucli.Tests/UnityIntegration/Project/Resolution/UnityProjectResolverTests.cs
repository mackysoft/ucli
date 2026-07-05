namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project.Resolution;

public sealed class UnityProjectResolverTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithValidUnityProjectPath_ReturnsResolvedContext ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "valid-path");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var resolver = CreateResolver();

        var result = resolver.Resolve(CreateCandidate(unityProjectPath));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProjectRoot);
        Assert.Equal(unityProjectPath, context.RepositoryRoot);
        Assert.Equal(UnityProjectPathSource.CommandOption, context.PathSource);
        Assert.Equal("6000.1.4f1", context.UnityVersion);
        Assert.Matches("^[0-9a-f]{64}$", context.ProjectFingerprint);
    }

    [Theory]
    [InlineData("serializedVersion: 1")]
    [InlineData("m_EditorVersion:")]
    [Trait("Size", "Medium")]
    public void Resolve_WhenUnityVersionCannotBeRead_ReturnsContextWithUnknownUnityVersion (string projectVersionContent)
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "unknown-unity-version");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(
            scope,
            "UnityProject",
            projectVersionContent);
        var resolver = CreateResolver();

        var result = resolver.Resolve(CreateCandidate(unityProjectPath));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        Assert.Equal(ProjectIdentityDefaults.UnknownUnityVersion, context.UnityVersion);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithRelativePath_NormalizesToAbsolutePath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "relative-path");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, unityProjectPath);
        var resolver = CreateResolver();

        var result = resolver.Resolve(CreateCandidate(relativePath));

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        FileSystemAssert.ForPath(context.UnityProjectRoot)
            .IsRooted()
            .EqualsNormalized(unityProjectPath);
        FileSystemAssert.ForPath(context.RepositoryRoot)
            .IsRooted()
            .EqualsNormalized(unityProjectPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithEnvironmentVariableCandidate_RetainsPathSource ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "environment-variable-source");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "EnvProject");
        var resolver = CreateResolver();

        var result = resolver.Resolve(new ProjectPathCandidate(
            unityProjectPath,
            UnityProjectPathSource.EnvironmentVariable,
            "UCLI_PROJECT_PATH"));

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProjectRoot);
        Assert.Equal(UnityProjectPathSource.EnvironmentVariable, context.PathSource);
        Assert.Equal("UCLI_PROJECT_PATH", context.PathSourceLabel);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_ReturnsInvalidArgument_WhenProjectDirectoryDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "missing-directory");
        var missingPath = scope.GetPath("MissingUnityProject");
        var resolver = CreateResolver();

        var result = resolver.Resolve(CreateCandidate(missingPath));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(ProjectContextErrorCodes.ProjectPathNotFound, error.Code);
        Assert.Contains("does not exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsInvalidArgument_WhenProjectPathFormatIsInvalid ()
    {
        var resolver = CreateResolver();

        var result = resolver.Resolve(CreateCandidate("invalid\0path"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(ProjectContextErrorCodes.ProjectPathInvalidFormat, error.Code);
        Assert.Contains("UnityProject path is invalid", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\0", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_ReturnsInvalidArgument_WhenProjectVersionFileIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "missing-project-version");
        scope.CreateDirectory(Path.Combine("UnityProject", "ProjectSettings"));
        var unityProjectPath = scope.GetPath("UnityProject");
        var resolver = CreateResolver();

        var result = resolver.Resolve(CreateCandidate(unityProjectPath));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(ProjectContextErrorCodes.UnityProjectMarkerMissing, error.Code);
        Assert.Contains("Missing file", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_ReturnsStableFingerprint_ForEquivalentPathRepresentations ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "fingerprint-stability");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var pathWithTrailingSeparator = unityProjectPath + Path.DirectorySeparatorChar;
        var resolver = CreateResolver();

        var primary = resolver.Resolve(CreateCandidate(unityProjectPath));
        var secondary = resolver.Resolve(CreateCandidate(pathWithTrailingSeparator));

        Assert.True(primary.IsSuccess);
        Assert.True(secondary.IsSuccess);
        Assert.Equal(primary.Context!.ProjectFingerprint, secondary.Context!.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithGitRootOnParentDirectory_UsesRepositoryRootForFingerprint ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "repository-root-parent");
        var repositoryRoot = scope.CreateDirectory("RepoRoot");
        scope.CreateDirectory(Path.Combine("RepoRoot", ".git"));
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, Path.Combine("RepoRoot", "UnityProject"));
        var resolver = CreateResolver();

        var result = resolver.Resolve(CreateCandidate(unityProjectPath));

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProjectRoot);
        Assert.Equal(repositoryRoot, context.RepositoryRoot);
        Assert.Matches("^[0-9a-f]{64}$", context.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Resolve_WithMultipleUnityProjectsUnderSameRepositoryRoot_ReturnsDifferentFingerprints ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "multiple-projects");
        var repositoryRoot = scope.CreateDirectory("RepoRoot");
        scope.CreateDirectory(Path.Combine("RepoRoot", ".git"));
        var primaryProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, Path.Combine("RepoRoot", "UnityProjectA"));
        var secondaryProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, Path.Combine("RepoRoot", "Packages", "UnityProjectB"));
        var resolver = CreateResolver();

        var primary = resolver.Resolve(CreateCandidate(primaryProjectPath));
        var secondary = resolver.Resolve(CreateCandidate(secondaryProjectPath));

        Assert.True(primary.IsSuccess);
        Assert.True(secondary.IsSuccess);
        Assert.Equal(repositoryRoot, primary.Context!.RepositoryRoot);
        Assert.Equal(repositoryRoot, secondary.Context!.RepositoryRoot);
        Assert.NotEqual(primary.Context!.ProjectFingerprint, secondary.Context!.ProjectFingerprint);
    }

    private static ProjectPathCandidate CreateCandidate (string projectPath)
    {
        return new ProjectPathCandidate(projectPath, UnityProjectPathSource.CommandOption, "--projectPath");
    }

    private static UnityProjectResolver CreateResolver ()
    {
        return new UnityProjectResolver();
    }
}
