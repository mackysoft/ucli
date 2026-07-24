using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Tests.Infrastructure.Paths;

public sealed class ProjectMutationAuditPathAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryFromRootRelativePath_WithPortablePath_ReturnsAuditPath ()
    {
        var path = RootRelativePath.Parse("Assets/Scenes/Main.unity");

        var succeeded = ProjectMutationAuditPathAdapter.TryFromRootRelativePath(
            path,
            out var auditPath);

        Assert.True(succeeded);
        Assert.NotNull(auditPath);
        Assert.Equal("Assets/Scenes/Main.unity", auditPath.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryFromRootRelativePath_OnUnixWithLiteralBackslash_ReturnsNoAuditPath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = RootRelativePath.Parse(@"Assets/literal\name.txt");

        var succeeded = ProjectMutationAuditPathAdapter.TryFromRootRelativePath(
            path,
            out var auditPath);

        Assert.False(succeeded);
        Assert.Null(auditPath);
    }
}
