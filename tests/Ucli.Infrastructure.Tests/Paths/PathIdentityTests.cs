using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Tests.Paths;

public sealed class PathIdentityTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsSamePath_WithTrailingSeparatorDifference_ReturnsTrue ()
    {
        var path = Path.GetFullPath(Path.Combine("path-identity", "same"));

        Assert.True(PathIdentity.IsSamePath(path, path + Path.DirectorySeparatorChar));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsSameOrChildPath_WithSamePath_ReturnsTrue ()
    {
        var rootPath = Path.GetFullPath(Path.Combine("path-identity", "root"));

        Assert.True(PathIdentity.IsSameOrChildPath(rootPath, rootPath + Path.DirectorySeparatorChar));
        Assert.False(PathIdentity.IsChildPath(rootPath, rootPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsChildPath_WithDescendant_ReturnsTrue ()
    {
        var rootPath = Path.GetFullPath(Path.Combine("path-identity", "root"));
        var childPath = Path.Combine(rootPath, "child", "file.txt");

        Assert.True(PathIdentity.IsSameOrChildPath(rootPath, childPath));
        Assert.True(PathIdentity.IsChildPath(rootPath, childPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsChildPath_WithSiblingPrefix_ReturnsFalse ()
    {
        var rootPath = Path.GetFullPath(Path.Combine("path-identity", "root"));
        var siblingPath = Path.GetFullPath(Path.Combine("path-identity", "root-sibling"));

        Assert.False(PathIdentity.IsSameOrChildPath(rootPath, siblingPath));
        Assert.False(PathIdentity.IsChildPath(rootPath, siblingPath));
    }
}
