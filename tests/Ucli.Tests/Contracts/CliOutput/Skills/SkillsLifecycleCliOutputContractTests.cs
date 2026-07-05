using System.Text.Json;
using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsLifecycleCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsProjectScope_WithBasicTierUpdateThenUninstall_CreatesNoOpsDeletesThenNoOps ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "update-uninstall-openai");
        var repoRoot = scope.CreateDirectory("repo");

        var created = await SkillsCliOutputContractTestSupport.RunOpenAiUpdateAsync(repoRoot);
        var updateNoOp = await SkillsCliOutputContractTestSupport.RunOpenAiUpdateAsync(repoRoot);

        using var createdJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(created.StdOut);
        Assert.Equal((int)CliExitCode.Success, created.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            createdJson.RootElement,
            UcliCommandNames.SkillsUpdate);
        JsonAssert.For(createdJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("scope", "project")
                .HasString("repositoryRoot", repoRoot)
                .HasValueKind("targetRoot", JsonValueKind.String)
                .HasValueKind("reloadGuidance", JsonValueKind.String)
                .HasArrayLength("actions", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasInt32("createdCount", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasInt32("updatedCount", 0)
                .HasInt32("noOpCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.ExpectedSkillNames[0])
                    .HasString("action", "created")));

        using var updateNoOpJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(updateNoOp.StdOut);
        Assert.Equal((int)CliExitCode.Success, updateNoOp.ExitCode);
        JsonAssert.For(updateNoOpJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("createdCount", 0)
                .HasInt32("updatedCount", 0)
                .HasInt32("noOpCount", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasValueKind("reloadGuidance", JsonValueKind.String)
                .HasProperty("actions", 0, static action => action
                    .HasString("action", "noOp")));

        var deleted = await SkillsCliOutputContractTestSupport.RunOpenAiUninstallAsync(repoRoot);
        var uninstallNoOp = await SkillsCliOutputContractTestSupport.RunOpenAiUninstallAsync(repoRoot);

        using var deletedJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(deleted.StdOut);
        Assert.Equal((int)CliExitCode.Success, deleted.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            deletedJson.RootElement,
            UcliCommandNames.SkillsUninstall);
        JsonAssert.For(deletedJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("scope", "project")
                .HasString("repositoryRoot", repoRoot)
                .HasArrayLength("actions", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasInt32("deletedCount", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasInt32("noOpCount", 0)
                .HasInt32("skippedUnmanagedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.ExpectedSkillNames[0])
                    .HasString("action", "deleted")));

        using var uninstallNoOpJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(uninstallNoOp.StdOut);
        Assert.Equal((int)CliExitCode.Success, uninstallNoOp.ExitCode);
        JsonAssert.For(uninstallNoOpJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("deletedCount", 0)
                .HasInt32("noOpCount", SkillsCliOutputContractTestSupport.ExpectedSkillNames.Length)
                .HasInt32("skippedUnmanagedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("action", "noOp")));
    }
}
