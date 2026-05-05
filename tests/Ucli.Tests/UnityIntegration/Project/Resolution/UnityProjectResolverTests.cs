namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project.Resolution;

public sealed class UnityProjectResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithValidUnityProjectPath_ReturnsResolvedContext ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "valid-path");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var resolver = new UnityProjectResolver();

        var result = resolver.Resolve(unityProjectPath);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProjectRoot);
        Assert.Equal(unityProjectPath, context.RepositoryRoot);
        Assert.Equal(UnityProjectPathSource.CommandOption, context.PathSource);
        Assert.Matches("^[0-9a-f]{64}$", context.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithRelativePath_NormalizesToAbsolutePath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "relative-path");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, unityProjectPath);
        var resolver = new UnityProjectResolver();

        var result = resolver.Resolve(relativePath);

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
    [Trait("Size", "Small")]
    public void Resolve_WhenCommandOptionIsMissing_UsesEnvironmentVariableProjectPath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "environment-variable-path");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "EnvProject");
        var resolver = CreateResolver(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = unityProjectPath,
        });

        var result = resolver.Resolve(null);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProjectRoot);
        Assert.Equal(UnityProjectPathSource.CommandOption, context.PathSource);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenCommandOptionIsSpecified_IgnoresEnvironmentVariableProjectPath ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "command-option-precedence");
        var commandProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "CommandProject");
        var environmentProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "EnvironmentProject");
        var resolver = CreateResolver(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = environmentProjectPath,
        });

        var result = resolver.Resolve(commandProjectPath);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        Assert.Equal(commandProjectPath, context.UnityProjectRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsInvalidArgument_WhenEnvironmentVariableProjectPathDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "environment-variable-missing-path");
        var missingPath = scope.GetPath("MissingUnityProject");
        var resolver = CreateResolver(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = missingPath,
        });

        var result = resolver.Resolve(null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("does not exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsInvalidArgument_WhenProjectDirectoryDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "missing-directory");
        var missingPath = scope.GetPath("MissingUnityProject");
        var resolver = new UnityProjectResolver();

        var result = resolver.Resolve(missingPath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("does not exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsInvalidArgument_WhenProjectVersionFileIsMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "missing-project-version");
        scope.CreateDirectory(Path.Combine("UnityProject", "ProjectSettings"));
        var unityProjectPath = scope.GetPath("UnityProject");
        var resolver = new UnityProjectResolver();

        var result = resolver.Resolve(unityProjectPath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("Missing file", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsStableFingerprint_ForEquivalentPathRepresentations ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "fingerprint-stability");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var pathWithTrailingSeparator = unityProjectPath + Path.DirectorySeparatorChar;
        var resolver = new UnityProjectResolver();

        var primary = resolver.Resolve(unityProjectPath);
        var secondary = resolver.Resolve(pathWithTrailingSeparator);

        Assert.True(primary.IsSuccess);
        Assert.True(secondary.IsSuccess);
        Assert.Equal(primary.Context!.ProjectFingerprint, secondary.Context!.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithGitRootOnParentDirectory_UsesRepositoryRootForFingerprint ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "repository-root-parent");
        var repositoryRoot = scope.CreateDirectory("RepoRoot");
        scope.CreateDirectory(Path.Combine("RepoRoot", ".git"));
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, Path.Combine("RepoRoot", "UnityProject"));
        var resolver = new UnityProjectResolver();

        var result = resolver.Resolve(unityProjectPath);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<ResolvedUnityProjectContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProjectRoot);
        Assert.Equal(repositoryRoot, context.RepositoryRoot);
        Assert.Matches("^[0-9a-f]{64}$", context.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithMultipleUnityProjectsUnderSameRepositoryRoot_ReturnsDifferentFingerprints ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-project-resolver", "multiple-projects");
        var repositoryRoot = scope.CreateDirectory("RepoRoot");
        scope.CreateDirectory(Path.Combine("RepoRoot", ".git"));
        var primaryProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, Path.Combine("RepoRoot", "UnityProjectA"));
        var secondaryProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, Path.Combine("RepoRoot", "Packages", "UnityProjectB"));
        var resolver = new UnityProjectResolver();

        var primary = resolver.Resolve(primaryProjectPath);
        var secondary = resolver.Resolve(secondaryProjectPath);

        Assert.True(primary.IsSuccess);
        Assert.True(secondary.IsSuccess);
        Assert.Equal(repositoryRoot, primary.Context!.RepositoryRoot);
        Assert.Equal(repositoryRoot, secondary.Context!.RepositoryRoot);
        Assert.NotEqual(primary.Context!.ProjectFingerprint, secondary.Context!.ProjectFingerprint);
    }

    private static UnityProjectResolver CreateResolver (IReadOnlyDictionary<string, string?> environmentVariables)
    {
        return new UnityProjectResolver(new ProjectPathInputResolver(new StubEnvironmentVariableReader(environmentVariables)));
    }
}
