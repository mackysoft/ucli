using MackySoft.Ucli.Hosting.Cli.Skills;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Skills;

public sealed class SkillsCommandArgumentNormalizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenSkillsCommandUsesLegacyOptions_RewritesGeneratedOptionNames ()
    {
        string[] args =
        [
            UcliCommandNames.Skills,
            UcliCommandNames.InstallSubcommand,
            "--repositoryRoot",
            "/repo-from-camel",
            "--repoRoot",
            "/repo",
            "--repo-root",
            "/repo-from-kebab",
            "--targetDir",
            ".agents/skills",
            "--dryRun",
            "--printDiff",
        ];

        var normalized = SkillsCommandArgumentNormalizer.Normalize(args);

        Assert.Equal(
            new[]
            {
                UcliCommandNames.Skills,
                UcliCommandNames.InstallSubcommand,
                "--repository-root",
                "/repo-from-camel",
                "--repository-root",
                "/repo",
                "--repository-root",
                "/repo-from-kebab",
                "--target-dir",
                ".agents/skills",
                "--dry-run",
                "--print-diff",
            },
            normalized);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenCommandIsNotSkills_ReturnsOriginalArguments ()
    {
        string[] args = [UcliCommandNames.Init, "--dryRun"];

        var normalized = SkillsCommandArgumentNormalizer.Normalize(args);

        Assert.Same(args, normalized);
    }
}
