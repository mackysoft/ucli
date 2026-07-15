namespace MackySoft.Ucli.Application.Tests.Shared.Context.Project;

public sealed class ResolvedUnityProjectContextTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenProjectFingerprintIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ResolvedUnityProjectContext.Create(
            unityProjectRoot: "/workspace/UnityProject",
            repositoryRoot: "/workspace",
            projectFingerprint: null!,
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion));

        Assert.Equal("projectFingerprint", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenPathsContainRedundantSegments_StoresCanonicalAbsolutePaths ()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ucli-context-tests"));

        var context = ResolvedUnityProjectContext.Create(
            unityProjectRoot: Path.Combine(root, "nested", "..", "UnityProject"),
            repositoryRoot: Path.Combine(root, "."),
            projectFingerprint: ProjectFingerprintTestFactory.Create("project"),
            pathSource: UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);

        Assert.Equal(Path.Combine(root, "UnityProject"), context.UnityProjectRoot);
        Assert.Equal(root, context.RepositoryRoot);
    }

    [Theory]
    [InlineData("UnityProject", "/workspace")]
    [InlineData("/workspace/UnityProject", "workspace")]
    [Trait("Size", "Small")]
    public void Create_WhenAPathIsRelative_ThrowsArgumentException (
        string unityProjectRoot,
        string repositoryRoot)
    {
        Assert.Throws<ArgumentException>(() => ResolvedUnityProjectContext.Create(
            unityProjectRoot,
            repositoryRoot,
            ProjectFingerprintTestFactory.Create("project"),
            UnityProjectPathSource.CommandOption,
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion));
    }
}
