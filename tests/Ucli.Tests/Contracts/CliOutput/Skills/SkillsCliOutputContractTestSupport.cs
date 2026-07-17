using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

internal static class SkillsCliOutputContractTestSupport
{
    public const string SelectedSingleSkillName = "ucli-read-project";

    private static readonly Lazy<ServiceProvider> SharedSkillsServiceProvider = new(UcliServiceProviderTestFactory.CreateSkillsFeature);

    private static readonly Lazy<SkillsCommandTestRunner> SharedSkillsRunner = new(() => new SkillsCommandTestRunner(SharedSkillsServiceProvider.Value));

    public static string[] ExpectedSkillNames =>
    [
        "ucli-plan-apply",
        "ucli-read-project",
        "ucli-troubleshoot",
        "ucli-verify-changes",
    ];

    public static ServiceProvider SharedServiceProvider => SharedSkillsServiceProvider.Value;

    public static SkillsCommandTestRunner SharedRunner => SharedSkillsRunner.Value;

    public static int ReadExpectedSkillBundleVersion ()
    {
        var descriptorPath = TestRepositoryPaths.GetFullPath("skills", "generated", "bundle.json");
        using var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
        return descriptor.RootElement.GetProperty("skillBundleVersion").GetInt32();
    }

    public static SkillsCommandTestRunner GetRunner (IServiceProvider? serviceProvider)
    {
        return serviceProvider is null
            ? SharedRunner
            : new SkillsCommandTestRunner(serviceProvider);
    }

    public static async Task<InstalledSkillFixture> InstallSelectedProjectSkillAsync (
        string repoRoot,
        IServiceProvider? serviceProvider = null,
        string host = "openai",
        string? targetDir = null)
    {
        CommandExecutionResult install = await RunProjectInstallAsync(
            repoRoot,
            host: host,
            targetDir: targetDir,
            serviceProvider: serviceProvider,
            skill: [SelectedSingleSkillName]);
        Assert.Equal((int)CliExitCode.Success, install.ExitCode);

        using JsonDocument installJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(install.StdOut);
        var targetRoot = installJson.RootElement.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        return new InstalledSkillFixture(targetRoot, SelectedSingleSkillName);
    }

    public static Task<CommandExecutionResult> RunOpenAiInstallAsync (
        string repoRoot,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false,
        IServiceProvider? serviceProvider = null,
        string[]? skill = null)
    {
        return RunProjectInstallAsync(
            repoRoot,
            host: "openai",
            targetDir: null,
            dryRun,
            force,
            printDiff,
            serviceProvider,
            skill);
    }

    public static Task<CommandExecutionResult> RunOpenAiUpdateAsync (
        string repoRoot,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false,
        IServiceProvider? serviceProvider = null,
        string[]? skill = null)
    {
        return GetRunner(serviceProvider).UpdateAsync(new SkillsCommandTestRunner.Options
        {
            Host = "openai",
            Scope = "project",
            RepoRoot = repoRoot,
            DryRun = dryRun,
            Force = force,
            PrintDiff = printDiff,
            Category = skill is null ? ["basic"] : null,
            Skill = skill,
        });
    }

    public static Task<CommandExecutionResult> RunOpenAiUninstallAsync (
        string repoRoot,
        bool dryRun = false,
        bool force = false,
        IServiceProvider? serviceProvider = null,
        string[]? skill = null)
    {
        return GetRunner(serviceProvider).UninstallAsync(new SkillsCommandTestRunner.Options
        {
            Host = "openai",
            Scope = "project",
            RepoRoot = repoRoot,
            DryRun = dryRun,
            Force = force,
            Category = skill is null ? ["basic"] : null,
            Skill = skill,
        });
    }

    public static Task<CommandExecutionResult> RunOpenAiPruneAsync (
        string repoRoot,
        bool dryRun = false,
        bool force = false,
        IServiceProvider? serviceProvider = null,
        string[]? skill = null)
    {
        return GetRunner(serviceProvider).PruneAsync(new SkillsCommandTestRunner.Options
        {
            Host = "openai",
            Scope = "project",
            RepoRoot = repoRoot,
            DryRun = dryRun,
            Force = force,
            Category = skill is null ? ["basic"] : null,
            Skill = skill,
        });
    }

    public static Task<CommandExecutionResult> RunProjectInstallAsync (
        string repoRoot,
        string host = "openai",
        string? targetDir = null,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false,
        IServiceProvider? serviceProvider = null,
        string[]? skill = null)
    {
        return GetRunner(serviceProvider).InstallAsync(new SkillsCommandTestRunner.Options
        {
            Host = host,
            Scope = "project",
            RepoRoot = repoRoot,
            TargetDir = targetDir,
            DryRun = dryRun,
            Force = force,
            PrintDiff = printDiff,
            Category = skill is null ? ["basic"] : null,
            Skill = skill,
        });
    }

    public static Task<CommandExecutionResult> RunOpenAiDoctorAsync (
        string repoRoot,
        IServiceProvider? serviceProvider = null,
        string[]? skill = null)
    {
        return GetRunner(serviceProvider).DoctorAsync(new SkillsCommandTestRunner.Options
        {
            Host = "openai",
            Scope = "project",
            RepoRoot = repoRoot,
            Category = skill is null ? ["basic"] : null,
            Skill = skill,
        });
    }

    public static Task<CommandExecutionResult> RunUserScopedCommandAsync (
        IServiceProvider serviceProvider,
        string subcommand,
        string targetRoot,
        string[]? skill = null)
    {
        return new SkillsCommandTestRunner(serviceProvider).ExecuteAsync(
            subcommand,
            new SkillsCommandTestRunner.Options
            {
                Host = "openai",
                Scope = "user",
                TargetDir = targetRoot,
                Category = skill is null ? ["basic"] : null,
                Skill = skill,
            });
    }

    public static Task<CommandExecutionResult> RunScopedCommandAsync (
        string subcommand,
        string repoRoot,
        string? host,
        string? scope,
        string? targetDir,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false)
    {
        return SharedRunner.ExecuteAsync(
            subcommand,
            new SkillsCommandTestRunner.Options
            {
                Host = host,
                Scope = scope,
                RepoRoot = repoRoot,
                TargetDir = targetDir,
                DryRun = dryRun,
                Force = force,
                PrintDiff = printDiff,
                Category = ["basic"],
            });
    }

    public static Task<CommandExecutionResult> RunProjectCommandWithoutRepoRootAsync (
        string subcommand,
        string workingDirectory,
        bool dryRun = false)
    {
        var args = new List<string>
        {
            UcliCommandNames.Skills,
            subcommand,
            "--host",
            "openai",
            "--category",
            "basic",
            "--scope",
            "project",
        };
        if (dryRun)
        {
            args.Add("--dryRun");
        }

        return CliInProcessRunner.RunCommandWithWorkingDirectoryAsync(workingDirectory, args.ToArray());
    }

    public static JsonElement FindAction (
        JsonElement root,
        string skillName)
    {
        return root.GetProperty("payload")
            .GetProperty("actions")
            .EnumerateArray()
            .Single(action => string.Equals(action.GetProperty("skillName").GetString(), skillName, StringComparison.Ordinal));
    }

    public static JsonElement FindDiffFile (
        JsonElement action,
        string relativePath)
    {
        return action.GetProperty("diffs")[0]
            .GetProperty("files")
            .EnumerateArray()
            .Single(file => string.Equals(file.GetProperty("relativePath").GetString(), relativePath, StringComparison.Ordinal));
    }

    public static JsonElement AssertSelectedSkillCreatedDryRunPlan (JsonElement root)
    {
        JsonAssert.For(root)
            .HasProperty("payload", payload => payload
                .HasBoolean("dryRun", true)
                .HasBoolean("force", false)
                .HasBoolean("printDiff", true)
                .HasInt32("createdCount", 1)
                .HasInt32("updatedCount", 0)
                .HasInt32("blockedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SelectedSingleSkillName)
                    .HasString("action", "created")
                    .IsNull("blockedReason")
                    .HasArrayLength("diffs", 1)));

        return FindAction(root, SelectedSingleSkillName);
    }

    public static void AssertSelectedSkillWasNotMaterialized (JsonElement root)
    {
        var targetRoot = root.GetProperty("payload").GetProperty("targetRoot").GetString()!;
        Assert.False(Directory.Exists(Path.Combine(targetRoot, SelectedSingleSkillName)));
    }

    public static JsonElement AssertSelectedSkillForcedUpdateWithDiff (JsonElement root)
    {
        JsonAssert.For(root)
            .HasProperty("payload", payload => payload
                .HasBoolean("force", true)
                .HasBoolean("printDiff", true)
                .HasInt32("updatedCount", 1)
                .HasInt32("blockedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SelectedSingleSkillName)
                    .HasString("action", "updated")
                    .IsNull("blockedReason")
                    .HasArrayLength("diffs", 1)));

        var action = FindAction(root, SelectedSingleSkillName);
        var modified = FindDiffFile(action, "SKILL.md");
        Assert.Equal("modified", modified.GetProperty("changeKind").GetString());
        Assert.Contains("Injected instruction.", modified.GetProperty("beforeContent").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Injected instruction.", modified.GetProperty("afterContent").GetString(), StringComparison.Ordinal);
        return action;
    }

    public readonly record struct InstalledSkillFixture (
        string TargetRoot,
        string SkillName)
    {
        public string SkillDirectory => Path.Combine(TargetRoot, SkillName);

        public string SkillMarkdownPath => GetPath("SKILL.md");

        public string GetPath (string relativePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
            return Path.Combine(SkillDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
