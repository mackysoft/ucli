using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Tests.Shared.Context.Project;

public sealed class ResolvedUnityProjectContextTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenProjectFingerprintIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ResolvedUnityProjectContext.Create(
            unityProjectRoot: AbsolutePath.Parse(ProjectPathTestValues.WorkspaceUnityProject),
            repositoryRoot: AbsolutePath.Parse(ProjectPathTestValues.WorkspaceRoot),
            projectFingerprint: null!,
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion));

        Assert.Equal("projectFingerprint", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithGuardedPaths_PreservesCanonicalValues ()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli-context-tests"));

        var context = ResolvedUnityProjectContext.Create(
            unityProjectRoot: AbsolutePath.Parse(Path.Combine(root, "nested", "..", "UnityProject")),
            repositoryRoot: AbsolutePath.Parse(Path.Combine(root, ".")),
            projectFingerprint: ProjectFingerprintTestFactory.Create("project"),
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);

        Assert.Equal(AbsolutePath.Parse(Path.Combine(root, "UnityProject")), context.UnityProjectRoot);
        Assert.Equal(AbsolutePath.Parse(root), context.RepositoryRoot);
    }
}
