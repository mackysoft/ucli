using MackySoft.Tests;
using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Installation;

public sealed class SkillUserTargetRootResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDefaultTargetRoot_UsesEnvironmentRootWhenPolicyHasNoChildDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "user-target-env-root");
        var environmentRoot = scope.GetPath("env-root");
        var resolver = new SkillUserTargetRootResolver(
            () => scope.GetPath("home"),
            name => string.Equals(name, "TEST_SKILLS_HOME", StringComparison.Ordinal) ? environmentRoot : null);
        var descriptor = new SkillHostDescriptor(
            "test",
            ".test/project-skills",
            "${TEST_SKILLS_HOME}",
            new SkillUserTargetRootPolicy("TEST_SKILLS_HOME", null, ".test/skills"),
            "Reload test skills.");

        var result = resolver.ResolveDefaultTargetRoot(descriptor);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(Path.GetFullPath(environmentRoot), result.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDefaultTargetRoot_RejectsEnvironmentChildDirectoryWithoutEnvironmentVariable ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "user-target-child-without-env");
        var resolver = new SkillUserTargetRootResolver(
            () => scope.GetPath("home"),
            static _ => null);
        var descriptor = new SkillHostDescriptor(
            "test",
            ".test/project-skills",
            "~/.test/skills",
            new SkillUserTargetRootPolicy(null, "skills", ".test/skills"),
            "Reload test skills.");

        var result = resolver.ResolveDefaultTargetRoot(descriptor);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.UserTargetUnavailable, result.Failure!.Code);
    }
}
