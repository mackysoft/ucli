using System.Text.Json;
using MackySoft.AgentSkills.Digests;
using MackySoft.AgentSkills.Installation.Validation;
using MackySoft.AgentSkills.Manifests;
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
            Tier = skill is null ? ["basic"] : null,
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
            Tier = skill is null ? ["basic"] : null,
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
            Tier = skill is null ? ["basic"] : null,
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
            Tier = skill is null ? ["basic"] : null,
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
            Tier = skill is null ? ["basic"] : null,
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
                Tier = skill is null ? ["basic"] : null,
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
                Tier = ["basic"],
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
            "--tier",
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

    public static Task RewriteInstalledSkillBodyAndManifestAsOlderAsync (InstalledSkillFixture installed)
    {
        return RewriteInstalledSkillBodyAndManifestAsOlderAsync(
            installed.TargetRoot,
            installed.SkillName);
    }

    private static async Task RewriteInstalledSkillBodyAndManifestAsOlderAsync (
        string targetRoot,
        string skillName)
    {
        var skillDirectory = Path.Combine(targetRoot, skillName);
        var skillPath = Path.Combine(skillDirectory, "SKILL.md");
        var manifestPath = Path.Combine(skillDirectory, "agent-skill.json");
        var skillText = NormalizeToLf(await File.ReadAllTextAsync(skillPath));
        Assert.True(SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter));
        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        var olderBody = body + "\nSynthetic previous version.\n";
        await File.WriteAllTextAsync(skillPath, frontmatter + "\n" + olderBody);

        var digestInputs = new List<SkillDigestInputFile>
        {
            new("SKILL.md", olderBody),
        };
        var referencesRoot = Path.Combine(skillDirectory, "references");
        if (Directory.Exists(referencesRoot))
        {
            foreach (var referencePath in Directory.EnumerateFiles(referencesRoot, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relativePath = Path.GetRelativePath(skillDirectory, referencePath).Replace(Path.DirectorySeparatorChar, '/');
                var content = NormalizeToLf(await File.ReadAllTextAsync(referencePath));
                digestInputs.Add(new SkillDigestInputFile(relativePath, content));
            }
        }

        var serializer = new SkillManifestJsonSerializer();
        var manifestResult = serializer.TryDeserialize(await File.ReadAllTextAsync(manifestPath));
        Assert.True(manifestResult.IsSuccess, manifestResult.Failure?.Message);
        var manifest = new SkillManifestDigestCalculator(serializer).WithComputedManifestDigest(manifestResult.Value! with
        {
            ContentDigest = new SkillDigestCalculator().ComputeDigest(digestInputs),
        });
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(manifest));
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

    private static string NormalizeToLf (string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
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
