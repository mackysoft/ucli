using MackySoft.Tests;
using MackySoft.Ucli.Skills.Distribution;

namespace MackySoft.Ucli.Skills.Tests.Distribution;

public sealed class BundledSkillPackageRootResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsSkillsDirectoryDirectlyUnderBaseDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "bundled-root");
        var baseDirectory = scope.CreateDirectory("app");
        var skillsDirectory = scope.CreateDirectory("app/skills");
        var resolver = new BundledSkillPackageRootResolver(baseDirectory);

        var result = resolver.Resolve();

        Assert.Equal(skillsDirectory, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_DoesNotWalkUpToParentSkillsDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "bundled-root-parent");
        var baseDirectory = scope.CreateDirectory("app");
        scope.CreateDirectory("skills");
        var resolver = new BundledSkillPackageRootResolver(baseDirectory);

        var exception = Assert.Throws<DirectoryNotFoundException>(resolver.Resolve);

        Assert.Contains(Path.Combine(baseDirectory, "skills"), exception.Message, StringComparison.Ordinal);
    }
}
