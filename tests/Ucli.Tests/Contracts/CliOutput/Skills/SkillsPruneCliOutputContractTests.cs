using MackySoft.AgentSkills.Distribution;
using MackySoft.Ucli.Hosting.Composition.Features;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsPruneCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsPrune_WithRemovedManagedSkill_DeletesOrphan ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "prune-removed-managed");
        var repoRoot = scope.CreateDirectory("repo");
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);
        var prunedCatalogBaseDirectory = CreatePackageBaseWithoutSkill(scope, SkillsCliOutputContractTestSupport.SelectedSingleSkillName);
        using var serviceProvider = CreateSkillsServiceProvider(prunedCatalogBaseDirectory);

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiPruneAsync(
            repoRoot,
            serviceProvider: serviceProvider,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.SkillsPrune);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("host", "openai")
                .HasString("scope", "project")
                .HasString("repositoryRoot", repoRoot)
                .HasBoolean("dryRun", false)
                .HasBoolean("force", false)
                .HasInt32("deletedCount", 1)
                .HasInt32("skippedCurrentCount", 0)
                .HasInt32("skippedForeignCatalogCount", 0)
                .HasInt32("skippedUnmanagedCount", 0)
                .HasInt32("blockedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.SelectedSingleSkillName)
                    .HasString("action", "deleted")
                    .IsNull("blockedReason")
                    .HasArrayLength("diffs", 0)));
        Assert.False(Directory.Exists(installed.SkillDirectory));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SkillsPrune_WithCurrentManagedSkill_SkipsCurrentWithoutDeleting ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-cli-output-contract", "prune-current-managed");
        var repoRoot = scope.CreateDirectory("repo");
        var installed = await SkillsCliOutputContractTestSupport.InstallSelectedProjectSkillAsync(repoRoot);

        var result = await SkillsCliOutputContractTestSupport.RunOpenAiPruneAsync(
            repoRoot,
            skill: [SkillsCliOutputContractTestSupport.SelectedSingleSkillName]);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasInt32("deletedCount", 0)
                .HasInt32("skippedCurrentCount", 1)
                .HasInt32("blockedCount", 0)
                .HasProperty("actions", 0, static action => action
                    .HasString("skillName", SkillsCliOutputContractTestSupport.SelectedSingleSkillName)
                    .HasString("action", "skippedCurrent")));
        Assert.True(Directory.Exists(installed.SkillDirectory));
    }

    private static ServiceProvider CreateSkillsServiceProvider (string packageBaseDirectory)
    {
        var services = new ServiceCollection();
        services.AddUcliSkillsFeatureServices();
        services.AddSingleton(_ => new BundledSkillPackageRootResolver(packageBaseDirectory));
        return services.BuildServiceProvider();
    }

    private static string CreatePackageBaseWithoutSkill (
        TestDirectoryScope scope,
        string removedSkillName)
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "skills");
        Assert.True(Directory.Exists(sourceRoot), $"Bundled test skills directory does not exist: {sourceRoot}");

        var baseDirectory = scope.CreateDirectory("pruned-catalog-base");
        var targetRoot = Path.Combine(baseDirectory, "skills");
        Directory.CreateDirectory(targetRoot);

        foreach (var skillDirectory in Directory.EnumerateDirectories(sourceRoot).Order(StringComparer.Ordinal))
        {
            var skillName = Path.GetFileName(skillDirectory);
            if (string.Equals(skillName, removedSkillName, StringComparison.Ordinal))
            {
                continue;
            }

            CopyDirectory(skillDirectory, Path.Combine(targetRoot, skillName));
        }

        return baseDirectory;
    }

    private static void CopyDirectory (
        string sourceDirectory,
        string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory).Order(StringComparer.Ordinal))
        {
            File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory).Order(StringComparer.Ordinal))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }
}
