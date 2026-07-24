using System.Text.Json;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsInstallCliOutputContractTests
{
    private const string InstallTargetHostConflictCode = "SKILL_INSTALL_TARGET_HOST_CONFLICT";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithProjectScope_CreatesThenNoOps ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-openai");
        var repoRoot = scope.CreateDirectory("repo");

        var created = await SkillsCliOutputContractTestSupport.RunOpenAiInstallAsync(repoRoot);
        var noOp = await SkillsCliOutputContractTestSupport.RunOpenAiInstallAsync(repoRoot);

        using var createdJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(created.StdOut);
        Assert.Equal((int)CliExitCode.Success, created.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            createdJson.RootElement,
            UcliCommandNames.SkillsInstall);
        JsonAssert.For(createdJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("scope", "project")
                .HasString("repositoryRoot", repoRoot)
                .HasValueKind("targetRoot", JsonValueKind.String)
                .HasArrayLength("actions", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasInt32("createdCount", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasInt32("noOpCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.ExpectedSkillNames[0])
                    .HasString("action", "created")));

        using var noOpJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(noOp.StdOut);
        Assert.Equal((int)CliExitCode.Success, noOp.ExitCode);
        JsonAssert.For(noOpJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("createdCount", 0)
                .HasInt32("noOpCount", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasProperty("actions", 0, static action => action
                    .HasString("action", "noOp")));

        var targetRoot = createdJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        Assert.EndsWith(Path.Combine(".agents", "skills"), targetRoot, StringComparison.Ordinal);
        foreach (var skillName in SkillsCliOutputContractTestSupport.ExpectedSkillNames)
        {
            Assert.True(File.Exists(Path.Combine(targetRoot, skillName, "SKILL.md")), skillName);
            Assert.True(File.Exists(Path.Combine(targetRoot, skillName, "agent-skill.json")), skillName);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithDryRunAndPrintDiff_ReturnsPlanWithoutWriting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-dry-run-diff");
        var repoRoot = scope.CreateDirectory("repo");

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiInstallAsync(
            repoRoot,
            dryRun: true,
            printDiff: true,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsInstall);
        var action = SkillsCliOutputContractTestSupport.AssertSelectedSkillCreatedDryRunPlan(outputJson.RootElement);
        JsonAssert.For(action)
            .HasProperty("diffs", 0, static diff => diff
                .HasProperty("files", 0, static file => file
                    .HasString("relativePath", "SKILL.md")
                    .HasString("changeKind", "added")
                    .IsNull("beforeContent")
                    .HasValueKind("afterContent", JsonValueKind.String)));
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasInt32("noOpCount", 0));
        SkillsCliOutputContractTestSupport.AssertSelectedSkillWasNotMaterialized(outputJson.RootElement);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithSharedTargetRootAcrossHosts_ReturnsHostConflict ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-host-conflict");
        var repoRoot = scope.CreateDirectory("repo");
        await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(
            repoRoot,
            host: "claude",
            targetDir: "shared-skills");

        var openAi = await SkillsCliOutputContractTestSupport.RunProjectInstallAsync(
            repoRoot,
            host: "openai",
            targetDir: "shared-skills",
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(openAi.StdOut);
        Assert.Equal((int)CliExitCode.ToolError, openAi.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.SkillsInstall,
            status: TextVocabulary.GetText(CommandResultStatus.Error),
            exitCode: (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InstallTargetHostConflictCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsInstall_WithForceAndPrintDiff_OverwritesLocalModification ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "install-force-local-modification");
        var repoRoot = scope.CreateDirectory("repo");
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        var skillPath = installed.SkillMarkdownPath;
        var localPath = installed.GetPath("local.md");
        await File.AppendAllTextAsync(skillPath, "\nInjected instruction.\n");
        await File.WriteAllTextAsync(localPath, "# Local\n");

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiInstallAsync(
            repoRoot,
            force: true,
            printDiff: true,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        var action = SkillsCliOutputContractTestSupport.AssertSelectedSkillForcedUpdateWithDiff(outputJson.RootElement);
        var deleted = SkillsCliOutputContractTestSupport.FindDiffFile(action, "local.md");
        Assert.Equal("deleted", deleted.GetProperty("changeKind").GetString());
        Assert.Equal("# Local\n", deleted.GetProperty("beforeContent").GetString());
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("afterContent").ValueKind);
        Assert.DoesNotContain("Injected instruction.", await File.ReadAllTextAsync(skillPath), StringComparison.Ordinal);
        Assert.False(File.Exists(localPath));
    }
}
