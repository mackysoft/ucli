using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsUninstallCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUninstall_WithUnmanagedOfficialDirectory_SkipsWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "uninstall-unmanaged");
        var repoRoot = scope.CreateDirectory("repo");
        var unmanagedPath = Path.Combine(repoRoot, ".agents", "skills", SkillsCliOutputContractTestSupport.SelectedSingleSkillName, "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(unmanagedPath)!);
        await File.WriteAllTextAsync(unmanagedPath, "# Existing\n");

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiUninstallAsync(repoRoot, skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("deletedCount", 0)
                .HasInt32("noOpCount", 0)
                .HasInt32("skippedUnmanagedCount", 1)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.SelectedSingleSkillName)
                    .HasString("action", "skippedUnmanaged")));
        Assert.True(File.Exists(unmanagedPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUninstall_WithDryRunLocalModification_ReturnsBlockedPlanWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "uninstall-dry-run-local-modification");
        var repoRoot = scope.CreateDirectory("repo");
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        await File.AppendAllTextAsync(installed.SkillMarkdownPath, "\nInjected instruction.\n");

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiUninstallAsync(
            repoRoot,
            dryRun: true,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("dryRun", true)
                .HasBoolean("force", false)
                .HasInt32("deletedCount", 0)
                .HasInt32("blockedCount", 1)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.SelectedSingleSkillName)
                    .HasString("action", "blockedLocalModification")
                    .HasString("blockedReason", "localModificationRequiresForce")
                    .HasArrayLength("diffs", 0)));
        Assert.True(Directory.Exists(installed.SkillDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUninstall_DoesNotModifyOtherHostTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "uninstall-other-host");
        var repoRoot = scope.CreateDirectory("repo");
        var openAi = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        var claude = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(
            repoRoot,
            host: "claude");

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiUninstallAsync(
            repoRoot,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.False(Directory.Exists(openAi.SkillDirectory));
        Assert.True(Directory.Exists(claude.SkillDirectory));
    }
}
