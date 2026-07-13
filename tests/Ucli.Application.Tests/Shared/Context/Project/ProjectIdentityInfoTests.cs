namespace MackySoft.Ucli.Application.Tests.Shared.Context.Project;

public sealed class ProjectIdentityInfoTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenProjectFingerprintIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ProjectIdentityInfo(
            ProjectPath: "/workspace/UnityProject",
            ProjectFingerprint: null!,
            UnityVersion: "6000.1.4f1"));

        Assert.Equal("ProjectFingerprint", exception.ParamName);
    }
}
