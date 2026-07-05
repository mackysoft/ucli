using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsUpdateCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithDryRunAndPrintDiff_ReturnsCreatedPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-dry-run-diff");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiUpdateAsync(
            repoRoot,
            dryRun: true,
            printDiff: true,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        SkillsCliOutputContractTestSupport.AssertSelectedSkillCreatedDryRunPlan(outputJson.RootElement);
        SkillsCliOutputContractTestSupport.AssertSelectedSkillWasNotMaterialized(outputJson.RootElement);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithCleanOutdatedTarget_ReturnsUpdatedAction ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-outdated");
        var repoRoot = scope.CreateDirectory("repo");
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        await SkillsCliOutputContractTestSupport.RewriteInstalledSkillBodyAndManifestAsOlderAsync(installed);

        var updated = await SkillsCliOutputContractTestSupport.RunOpenAiUpdateAsync(
            repoRoot,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var updatedJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(updated.StdOut);
        Assert.Equal((int)CliExitCode.Success, updated.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            updatedJson.RootElement,
            UcliCommandNames.SkillsUpdate);
        JsonAssert.For(updatedJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("createdCount", 0)
                .HasInt32("updatedCount", 1)
                .HasInt32("noOpCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.SelectedSingleSkillName)
                    .HasString("action", "updated")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithDryRunLocalModification_ReturnsBlockedPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-dry-run-local-modification");
        var repoRoot = scope.CreateDirectory("repo");
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        var skillPath = installed.SkillMarkdownPath;
        await File.AppendAllTextAsync(skillPath, "\nInjected instruction.\n");
        var modifiedSkill = await File.ReadAllTextAsync(skillPath);

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiUpdateAsync(
            repoRoot,
            dryRun: true,
            printDiff: true,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasBoolean("dryRun", true)
                .HasBoolean("force", false)
                .HasBoolean("printDiff", true)
                .HasInt32("updatedCount", 0)
                .HasInt32("blockedCount", 1)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.SelectedSingleSkillName)
                    .HasString("action", "blockedLocalModification")
                    .HasString("blockedReason", "localModificationRequiresForce")
                    .HasArrayLength("diffs", 1)));
        Assert.Equal(modifiedSkill, await File.ReadAllTextAsync(skillPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUpdate_WithForceAndPrintDiff_OverwritesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-force-local-modification");
        var repoRoot = scope.CreateDirectory("repo");
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        var skillPath = installed.SkillMarkdownPath;
        await File.AppendAllTextAsync(skillPath, "\nInjected instruction.\n");

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiUpdateAsync(
            repoRoot,
            force: true,
            printDiff: true,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasBoolean("dryRun", false)
                .HasBoolean("force", true));
        SkillsCliOutputContractTestSupport.AssertSelectedSkillForcedUpdateWithDiff(outputJson.RootElement);
        Assert.DoesNotContain("Injected instruction.", await File.ReadAllTextAsync(skillPath), StringComparison.Ordinal);
    }
}
