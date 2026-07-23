using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Shared.Context.Project;

public sealed class ProjectIdentityInfoTests
{
    private static readonly ProjectFingerprint Fingerprint = ProjectFingerprintTestFactory.Create("project-identity-info");

    [Fact]
    [Trait("Size", "Small")]
    public void From_WhenProjectIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ProjectIdentityInfo.From(null!));

        Assert.Equal("project", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void From_ProjectsResolvedContext ()
    {
        var resolvedProject = CreateResolvedProject();

        var result = ProjectIdentityInfo.From(resolvedProject);

        Assert.Equal(resolvedProject.UnityProjectRoot.Value, result.ProjectPath);
        Assert.Equal(resolvedProject.ProjectFingerprint, result.ProjectFingerprint);
        Assert.Equal(resolvedProject.UnityVersion, result.UnityVersion);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryFromHost_WhenIdentityMatches_ReturnsCanonicalLocalIdentity ()
    {
        var resolvedProject = CreateResolvedProject();
        var hostProject = new IpcProjectIdentity(
            projectPath: resolvedProject.UnityProjectRoot.Value + Path.DirectorySeparatorChar,
            projectFingerprint: resolvedProject.ProjectFingerprint,
            unityVersion: resolvedProject.UnityVersion);

        var succeeded = ProjectIdentityInfo.TryFromHost(
            resolvedProject,
            hostProject,
            out var result,
            out _);

        Assert.True(succeeded);
        Assert.Equal(ProjectIdentityInfo.From(resolvedProject), result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryFromHost_WhenExpectedUnityVersionIsUnknown_UsesHostVersion ()
    {
        var resolvedProject = CreateResolvedProject(unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
        var hostProject = new IpcProjectIdentity(
            projectPath: resolvedProject.UnityProjectRoot.Value,
            projectFingerprint: resolvedProject.ProjectFingerprint,
            unityVersion: "6000.1.4f1");

        var succeeded = ProjectIdentityInfo.TryFromHost(
            resolvedProject,
            hostProject,
            out var result,
            out _);

        Assert.True(succeeded);
        Assert.Equal(hostProject.UnityVersion, Assert.IsType<ProjectIdentityInfo>(result).UnityVersion);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)ProjectIdentityMismatchKind.ProjectFingerprint)]
    [InlineData((int)ProjectIdentityMismatchKind.ProjectPath)]
    [InlineData((int)ProjectIdentityMismatchKind.UnityVersion)]
    public void TryFromHost_WhenIdentityDiffers_ClassifiesMismatch (int expectedMismatchValue)
    {
        var expectedMismatchKind = (ProjectIdentityMismatchKind)expectedMismatchValue;
        var resolvedProject = CreateResolvedProject();
        var hostProject = new IpcProjectIdentity(
            projectPath: expectedMismatchKind == ProjectIdentityMismatchKind.ProjectPath
                ? Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli-tests", "DifferentUnityProject"))
                : resolvedProject.UnityProjectRoot.Value,
            projectFingerprint: expectedMismatchKind == ProjectIdentityMismatchKind.ProjectFingerprint
                ? ProjectFingerprintTestFactory.Create("different-project")
                : resolvedProject.ProjectFingerprint,
            unityVersion: expectedMismatchKind == ProjectIdentityMismatchKind.UnityVersion
                ? "different-version"
                : resolvedProject.UnityVersion);

        var succeeded = ProjectIdentityInfo.TryFromHost(
            resolvedProject,
            hostProject,
            out var result,
            out var mismatchKind);

        Assert.False(succeeded);
        Assert.Null(result);
        Assert.Equal(expectedMismatchKind, mismatchKind);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative/UnityProject")]
    [Trait("Size", "Small")]
    public void TryFromHost_WhenProjectPathWireTextIsInvalid_ClassifiesProjectPathMismatch (
        string projectPath)
    {
        var resolvedProject = CreateResolvedProject();
        var hostProject = new IpcProjectIdentity(
            projectPath,
            resolvedProject.ProjectFingerprint,
            resolvedProject.UnityVersion);

        var succeeded = ProjectIdentityInfo.TryFromHost(
            resolvedProject,
            hostProject,
            out var result,
            out var mismatchKind);

        Assert.False(succeeded);
        Assert.Null(result);
        Assert.Equal(ProjectIdentityMismatchKind.ProjectPath, mismatchKind);
    }

    private static ResolvedUnityProjectContext CreateResolvedProject (
        string unityVersion = "6000.1.4f1")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli-tests"));
        return ResolvedUnityProjectContext.Create(
            unityProjectRoot: AbsolutePath.Parse(Path.Combine(repositoryRoot, "UnityProject")),
            repositoryRoot: AbsolutePath.Parse(repositoryRoot),
            projectFingerprint: Fingerprint,
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: unityVersion);
    }
}
