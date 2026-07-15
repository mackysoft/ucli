using System.Text.Json;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsScopeCliOutputContractTests
{
    private const string PathUnsafeCode = "SKILL_PATH_UNSAFE";

    private static readonly (string Subcommand, string ExpectedCommand)[] ProjectSubcommandCases =
    [
        (UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall),
        (UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate),
        (UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor),
        (UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall),
    ];

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsProjectScope_WithoutRepoRoot_UsesWorkingDirectoryGitRootForProjectSubcommands ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cwd-contract", "project-default-repo-root");
        var repoRoot = scope.CreateDirectory("repo");
        scope.CreateDirectory(Path.Combine("repo", ".git"));
        var workingDirectory = scope.CreateDirectory(Path.Combine("repo", "src", "tool"));
        var seed = await SkillsCliOutputContractTestSupport.SharedRunner.InstallAsync(new SkillsCommandTestRunner.Options
        {
            Host = "openai",
            Scope = "project",
            RepoRoot = repoRoot,
            Tier = ["basic"],
        });
        Assert.Equal((int)CliExitCode.Success, seed.ExitCode);

        foreach (var (subcommand, expectedCommand) in ProjectSubcommandCases)
        {
            var result = await SkillsCliOutputContractTestSupport.RunProjectCommandWithoutRepoRootAsync(
                subcommand,
                workingDirectory);

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.Equal((int)CliExitCode.Success, result.ExitCode);
            CommandResultAssert.HasSuccessEnvelope(
                outputJson.RootElement,
                expectedCommand);
            var payload = outputJson.RootElement.GetProperty("payload");
            FileSystemAssert.ForPath(payload.GetProperty("repositoryRoot").GetString()!).EqualsNormalized(repoRoot);
            FileSystemAssert.ForPath(payload.GetProperty("targetRoot").GetString()!).EqualsNormalized(Path.Combine(repoRoot, ".agents", "skills"));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsProjectScope_WithoutRepoRootAndGitMarker_UsesWorkingDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "project-default-repo-root-no-git");
        var workingDirectory = scope.CreateDirectory("workspace");

        var result = await SkillsCliOutputContractTestSupport.RunProjectCommandWithoutRepoRootAsync(
            UcliCommandNames.InstallSubcommand,
            workingDirectory,
            dryRun: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsInstall);
        var payload = outputJson.RootElement.GetProperty("payload");
        FileSystemAssert.ForPath(payload.GetProperty("repositoryRoot").GetString()!).EqualsNormalized(workingDirectory);
        FileSystemAssert.ForPath(payload.GetProperty("targetRoot").GetString()!).EqualsNormalized(Path.Combine(workingDirectory, ".agents", "skills"));
        FileSystemAssert.ForPath(Path.Combine(workingDirectory, ".agents")).DoesNotExist();
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUserScope_WithoutTargetDir_UsesOpenAiCodexHomeDefault ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "user-scope-codex-home-default");
        var codexHome = scope.GetPath("codex-home");

        var install = await CliInProcessRunner.RunCommandWithEnvironmentAsync(
            new Dictionary<string, string?> { ["CODEX_HOME"] = codexHome },
            UcliCommandNames.Skills,
            UcliCommandNames.InstallSubcommand,
            "--host",
            "openai",
            "--skill",
            SkillsCliOutputContractTestSupport.SelectedSingleSkillName,
            "--scope",
            "user");

        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        JsonAssert.For(installJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasInt32("createdCount", 1)
                .HasValueKind("reloadGuidance", JsonValueKind.String));
        var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        Assert.True(Path.IsPathFullyQualified(targetRoot), targetRoot);
        Assert.EndsWith(Path.Combine("codex-home", "skills"), targetRoot, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsUserScope_WithExplicitTarget_InstallsUpdatesDoctorsAndUninstallsWithoutRepoRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "user-scope-explicit-target");
        var targetRoot = scope.GetPath("user-skills");
        var serviceProvider = SkillsCliOutputContractTestSupport.SharedServiceProvider;
        string[] selectedSkill = [SkillsCliOutputContractTestSupport.SelectedSingleSkillName];

        var install = await SkillsCliOutputContractTestSupport.RunUserScopedCommandAsync(
            serviceProvider,
            UcliCommandNames.InstallSubcommand,
            targetRoot,
            selectedSkill);
        var update = await SkillsCliOutputContractTestSupport.RunUserScopedCommandAsync(
            serviceProvider,
            UcliCommandNames.UpdateSubcommand,
            targetRoot,
            selectedSkill);
        var doctor = await SkillsCliOutputContractTestSupport.RunUserScopedCommandAsync(
            serviceProvider,
            UcliCommandNames.DoctorSubcommand,
            targetRoot,
            selectedSkill);
        var uninstall = await SkillsCliOutputContractTestSupport.RunUserScopedCommandAsync(
            serviceProvider,
            UcliCommandNames.UninstallSubcommand,
            targetRoot,
            selectedSkill);

        using var installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);
        JsonAssert.For(installJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasValueKind("targetRoot", JsonValueKind.String)
                .HasInt32("createdCount", 1)
                .HasValueKind("reloadGuidance", JsonValueKind.String));
        var resolvedTargetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        Assert.True(Path.IsPathFullyQualified(resolvedTargetRoot), resolvedTargetRoot);
        Assert.EndsWith(Path.DirectorySeparatorChar + Path.GetFileName(targetRoot), resolvedTargetRoot, StringComparison.Ordinal);

        using var updateJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(update.StdOut);
        Assert.Equal((int)CliExitCode.Success, update.ExitCode);
        JsonAssert.For(updateJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasInt32("createdCount", 0)
                .HasInt32("noOpCount", 1)
                .HasValueKind("reloadGuidance", JsonValueKind.String));

        using var doctorJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(doctor.StdOut);
        Assert.Equal((int)CliExitCode.Success, doctor.ExitCode);
        JsonAssert.For(doctorJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasBoolean("isHealthy", true)
                .HasValueKind("reloadGuidance", JsonValueKind.String));

        using var uninstallJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(uninstall.StdOut);
        Assert.Equal((int)CliExitCode.Success, uninstall.ExitCode);
        JsonAssert.For(uninstallJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("scope", "user")
                .IsNull("repositoryRoot")
                .HasInt32("deletedCount", 1)
                .HasValueKind("reloadGuidance", JsonValueKind.String));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsScopedSubcommands_WithTargetDirOutsideRepositoryRoot_ReturnPathUnsafe ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "outside-target");
        using var outsideScope = TestDirectories.CreateTempScope("skills-cli-output-contract", "outside-target-root");
        var repoRoot = scope.CreateDirectory("repo");

        foreach (var (subcommand, expectedCommand) in ProjectSubcommandCases)
        {
            var result = await SkillsCliOutputContractTestSupport.RunScopedCommandAsync(
                subcommand,
                repoRoot,
                host: "openai",
                scope: "project",
                targetDir: outsideScope.FullPath);

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
            CommandResultAssert.HasInvalidArgumentEnvelope(
                outputJson.RootElement,
                expectedCommand);
            CommandResultAssert.HasSingleError(outputJson.RootElement, PathUnsafeCode);
        }
    }
}
