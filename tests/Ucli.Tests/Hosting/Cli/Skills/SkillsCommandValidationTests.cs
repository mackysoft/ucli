namespace MackySoft.Ucli.Tests;

public sealed class SkillsCommandValidationTests
{
    private const string HostUnsupportedCode = "SKILL_HOST_UNSUPPORTED";

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ExportSubcommand, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.PruneSubcommand, UcliCommandNames.SkillsPrune)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task Execute_WhenHostIsMissing_ReturnsInvalidArgumentBeforePackageResolution (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-command-validation", $"missing-host-{subcommand}");
        var repoRoot = GetValidationPath(scope, subcommand, "repo");
        var outputRoot = GetValidationPath(scope, subcommand, "exported");

        var result = await ExecuteWithoutHostAsync(subcommand, repoRoot, outputRoot);

        AssertInvalidArgument(result, expectedCommand, "Option '--host' is required.");
        AssertValidationPathsWereNotCreated(repoRoot, outputRoot);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.PruneSubcommand, UcliCommandNames.SkillsPrune)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task Execute_WhenScopeIsMissing_ReturnsInvalidArgumentBeforePackageResolution (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-command-validation", $"missing-scope-{subcommand}");
        var repoRoot = GetValidationPath(scope, subcommand, "repo");

        var result = await ExecuteScopedCommandAsync(subcommand, repoRoot, scope: null);

        AssertInvalidArgument(result, expectedCommand, "Option '--scope' is required.");
        AssertValidationPathsWereNotCreated(repoRoot);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.PruneSubcommand, UcliCommandNames.SkillsPrune)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task Execute_WhenScopeIsUnsupported_ReturnsInvalidArgumentBeforePackageResolution (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-command-validation", $"invalid-scope-{subcommand}");
        var repoRoot = GetValidationPath(scope, subcommand, "repo");

        var result = await ExecuteScopedCommandAsync(subcommand, repoRoot, scope: "global");

        AssertInvalidArgument(result, expectedCommand, "Unsupported SKILL scope: global.");
        AssertValidationPathsWereNotCreated(repoRoot);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.PruneSubcommand, UcliCommandNames.SkillsPrune)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task Execute_WhenUserScopeSpecifiesRepoRoot_ReturnsInvalidArgumentBeforePackageResolution (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-command-validation", $"user-repo-root-{subcommand}");
        var repoRoot = GetValidationPath(scope, subcommand, "repo");

        var result = await ExecuteScopedCommandAsync(subcommand, repoRoot, scope: "user");

        AssertInvalidArgument(result, expectedCommand, "Option '--repoRoot' is not supported when '--scope user' is used.");
        AssertValidationPathsWereNotCreated(repoRoot);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ExportSubcommand, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.PruneSubcommand, UcliCommandNames.SkillsPrune)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task Execute_WhenPackageSelectionIsMissing_ReturnsInvalidArgumentBeforePackageResolution (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-command-validation", $"missing-selection-{subcommand}");
        var repoRoot = GetValidationPath(scope, subcommand, "repo");
        var outputRoot = GetValidationPath(scope, subcommand, "exported");

        var result = await ExecuteWithoutPackageSelectionAsync(subcommand, repoRoot, outputRoot);

        AssertInvalidArgument(result, expectedCommand, "Option '--category' or '--skill' is required.");
        AssertValidationPathsWereNotCreated(repoRoot, outputRoot);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ExportSubcommand, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task Execute_WhenCategoryIsNotInBundle_ReturnsInvalidArgumentBeforeTargetResolution (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-command-validation", $"unavailable-category-{subcommand}");
        var repoRoot = scope.GetPath(Path.Combine(subcommand, "repo"));
        var outputRoot = scope.GetPath(Path.Combine(subcommand, "exported"));

        var result = await ExecuteUnavailableCategoryAsync(
            subcommand,
            repoRoot,
            outputRoot);

        AssertInvalidArgument(result, expectedCommand, "Unsupported SKILL category: advanced.");
        AssertValidationPathsWereNotCreated(repoRoot, outputRoot);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Prune_WhenCategoryWasRemovedFromBundle_ReturnsSuccessfulEmptyResult ()
    {
        using var scope = TestDirectories.CreateTempScope("skills-command-validation", "removed-category-prune");

        var result = await ExecuteUnavailableCategoryAsync(
            UcliCommandNames.PruneSubcommand,
            scope.GetPath("repo"),
            scope.GetPath("exported"));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(outputJson.RootElement, UcliCommandNames.SkillsPrune);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasArrayLength("categories", 1)
                .HasArrayLength("skillNames", 0)
                .HasArrayLength("actions", 0));
        Assert.Equal("advanced", outputJson.RootElement.GetProperty("payload").GetProperty("categories")[0].GetString());
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ExportSubcommand, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.UpdateSubcommand, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.UninstallSubcommand, UcliCommandNames.SkillsUninstall)]
    [InlineData(UcliCommandNames.PruneSubcommand, UcliCommandNames.SkillsPrune)]
    [InlineData(UcliCommandNames.DoctorSubcommand, UcliCommandNames.SkillsDoctor)]
    public async Task Execute_WhenHostIsUnsupported_ReturnsHostUnsupportedBeforeTargetResolution (
        string subcommand,
        string expectedCommand)
    {
        using var scope = TestDirectories.CreateTempScope("skills-command-validation", $"unsupported-host-{subcommand}");
        var repoRoot = GetValidationPath(scope, subcommand, "repo");
        var outputRoot = GetValidationPath(scope, subcommand, "exported");

        var result = await ExecuteWithUnsupportedHostAsync(subcommand, repoRoot, outputRoot);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            expectedCommand);
        CommandResultAssert.HasSingleError(outputJson.RootElement, HostUnsupportedCode);
        AssertValidationPathsWereNotCreated(repoRoot, outputRoot);
    }

    private static Task<CommandExecutionResult> ExecuteWithoutHostAsync (
        string subcommand,
        string repoRoot,
        string outputRoot)
    {
        return SkillsCliOutputContractTestSupport.SharedRunner.ExecuteAsync(
            subcommand,
            new SkillsCommandTestRunner.Options
            {
                Host = null,
                Output = outputRoot,
                Scope = "project",
                RepoRoot = repoRoot,
                Category = ["basic"],
            });
    }

    private static Task<CommandExecutionResult> ExecuteWithoutPackageSelectionAsync (
        string subcommand,
        string repoRoot,
        string outputRoot)
    {
        return SkillsCliOutputContractTestSupport.SharedRunner.ExecuteAsync(
            subcommand,
            new SkillsCommandTestRunner.Options
            {
                Host = "openai",
                Output = outputRoot,
                Scope = "project",
                RepoRoot = repoRoot,
            });
    }

    private static Task<CommandExecutionResult> ExecuteUnavailableCategoryAsync (
        string subcommand,
        string repoRoot,
        string outputRoot)
    {
        return SkillsCliOutputContractTestSupport.SharedRunner.ExecuteAsync(
            subcommand,
            new SkillsCommandTestRunner.Options
            {
                Host = "openai",
                Output = outputRoot,
                Scope = "project",
                RepoRoot = repoRoot,
                DryRun = true,
                Category = ["advanced"],
            });
    }

    private static Task<CommandExecutionResult> ExecuteWithUnsupportedHostAsync (
        string subcommand,
        string repoRoot,
        string outputRoot)
    {
        return SkillsCliOutputContractTestSupport.SharedRunner.ExecuteAsync(
            subcommand,
            new SkillsCommandTestRunner.Options
            {
                Host = "generic",
                Output = outputRoot,
                Scope = "project",
                RepoRoot = repoRoot,
                Category = ["basic"],
            });
    }

    private static Task<CommandExecutionResult> ExecuteScopedCommandAsync (
        string subcommand,
        string repoRoot,
        string? scope)
    {
        return SkillsCliOutputContractTestSupport.SharedRunner.ExecuteAsync(
            subcommand,
            new SkillsCommandTestRunner.Options
            {
                Host = "openai",
                Scope = scope,
                RepoRoot = repoRoot,
                Category = ["basic"],
            });
    }

    private static string GetValidationPath (
        TestDirectoryScope scope,
        string subcommand,
        string name)
    {
        return scope.GetPath(Path.Combine(subcommand, name));
    }

    private static void AssertValidationPathsWereNotCreated (params string[] paths)
    {
        foreach (string path in paths)
        {
            FileSystemAssert.ForPath(path).DoesNotExist();
        }
    }

    private static void AssertInvalidArgument (
        CommandExecutionResult result,
        string expectedCommand,
        string expectedMessageFragment)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, expectedCommand);
        Assert.Contains(expectedMessageFragment, outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }
}
