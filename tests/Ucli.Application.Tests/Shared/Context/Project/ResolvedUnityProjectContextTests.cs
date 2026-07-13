using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Application.Tests.Shared.Context.Project;

public sealed class ResolvedUnityProjectContextTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenProjectFingerprintIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ResolvedUnityProjectContext(
            UnityProjectRoot: "/workspace/UnityProject",
            RepositoryRoot: "/workspace",
            ProjectFingerprint: null!,
            PathSource: UnityProjectPathSource.CommandOption));

        Assert.Equal("ProjectFingerprint", exception.ParamName);
    }
}
