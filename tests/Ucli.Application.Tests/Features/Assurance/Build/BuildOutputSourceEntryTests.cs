using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildOutputSourceEntryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FromAbsolutePath_WhenPathIsNull_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => BuildOutputSourceEntry.FromAbsolutePath(null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromAbsolutePath_StoresGuardedPath ()
    {
        var path = AbsolutePath.Parse(
            Path.Combine(Path.GetTempPath(), "ucli-build-source", "nested", "..", "player"));

        var source = BuildOutputSourceEntry.FromAbsolutePath(path);

        var absolute = Assert.IsType<BuildOutputSourceEntry.Absolute>(source);
        Assert.Same(path, absolute.Path);
    }
}
