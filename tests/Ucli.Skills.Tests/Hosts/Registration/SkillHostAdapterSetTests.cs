using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Hosts.Registration;

public sealed class SkillHostAdapterSetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AdapterSet_UsesInjectedAdaptersOnlyInDeterministicOrder ()
    {
        var adapterSet = new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter("beta", ".beta/skills"),
            new TestSkillHostAdapter("alpha", ".alpha/skills"),
        ]);

        Assert.Equal(
            new[] { "alpha", "beta" },
            adapterSet.Adapters.Select(static adapter => adapter.Descriptor.HostKey).ToArray());

        Assert.Equal(".alpha/skills", adapterSet.GetAdapter("alpha").Value!.Descriptor.ProjectTargetDirectory);
        Assert.Equal(".beta/skills", adapterSet.GetAdapter("beta").Value!.Descriptor.ProjectTargetDirectory);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetAdapter_ReturnsUnsupportedHostFailure ()
    {
        var adapterSet = new SkillHostAdapterSet([new TestSkillHostAdapter("alpha", ".alpha/skills")]);

        var result = adapterSet.GetAdapter("generic");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetAdapter_CanonicalizesHostKey ()
    {
        var adapterSet = new SkillHostAdapterSet([new TestSkillHostAdapter("openai", ".agents/skills")]);

        var result = adapterSet.GetAdapter("OpenAI");

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("openai", result.Value!.Descriptor.HostKey);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_AllowsEnvironmentVariableRootWithoutChildDirectory ()
    {
        var adapterSet = new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(
                "alpha",
                ".alpha/skills",
                new SkillUserTargetRootPolicy("ALPHA_HOME", null, ".alpha/skills")),
        ]);

        Assert.Equal("alpha", adapterSet.Adapters.Single().Descriptor.HostKey);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsUnsafeHomeRelativeUserTargetDirectory ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(
                "alpha",
                ".alpha/skills",
                new SkillUserTargetRootPolicy(null, null, "../outside")),
        ]));

        Assert.Contains("home-relative user target directory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsEnvironmentChildDirectoryWithoutEnvironmentVariable ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter(
                "alpha",
                ".alpha/skills",
                new SkillUserTargetRootPolicy(null, "skills", ".alpha/skills")),
        ]));

        Assert.Contains("requires an environment variable name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsEmptyAdapters ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet([]));

        Assert.Contains("At least one host adapter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_RejectsDuplicateHostKeys ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkillHostAdapterSet(
        [
            new TestSkillHostAdapter("openai", ".agents/skills"),
            new TestSkillHostAdapter("OpenAI", ".other/skills"),
        ]));

        Assert.Contains("Host adapter key must be unique", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestSkillHostAdapter : ISkillHostAdapter
    {
        public TestSkillHostAdapter (
            string hostKey,
            string projectTargetDirectory,
            SkillUserTargetRootPolicy? userTargetRootPolicy = null)
        {
            Descriptor = new SkillHostDescriptor(
                hostKey,
                projectTargetDirectory,
                "~/.test/skills",
                userTargetRootPolicy ?? new SkillUserTargetRootPolicy(null, null, ".test/skills"),
                "Reload test skills.");
        }

        public SkillHostDescriptor Descriptor { get; }

        public string? MetadataArtifactPath => null;

        public SkillHostArtifactSet BuildArtifacts (SkillHostMetadata metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            return new SkillHostArtifactSet(string.Empty, null);
        }
    }
}
