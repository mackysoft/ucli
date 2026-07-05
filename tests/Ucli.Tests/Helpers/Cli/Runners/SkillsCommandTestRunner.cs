using MackySoft.Ucli.Hosting.Cli.Skills;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Tests;

internal sealed class SkillsCommandTestRunner
{
    private readonly IServiceProvider serviceProvider;

    public SkillsCommandTestRunner (IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<CommandExecutionResult> ListAsync (
        string[]? tier = null,
        string[]? skill = null)
    {
        return CommandResultCapture.ExecuteAsync(() =>
            CreateCommand<SkillsListCommand>().ListAsync(tier, skill));
    }

    public Task<CommandExecutionResult> ExecuteAsync (
        string subcommand,
        Options options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subcommand);

        return subcommand switch
        {
            UcliCommandNames.ExportSubcommand => ExportAsync(options),
            UcliCommandNames.InstallSubcommand => InstallAsync(options),
            UcliCommandNames.UpdateSubcommand => UpdateAsync(options),
            UcliCommandNames.UninstallSubcommand => UninstallAsync(options),
            UcliCommandNames.DoctorSubcommand => DoctorAsync(options),
            _ => throw new ArgumentOutOfRangeException(nameof(subcommand), subcommand, "Unsupported skills subcommand."),
        };
    }

    public Task<CommandExecutionResult> ExportAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return CommandResultCapture.ExecuteAsync(() =>
            CreateCommand<SkillsExportCommand>().ExportAsync(
                host: options.Host,
                output: options.Output,
                format: options.Format,
                tier: options.Tier,
                skill: options.Skill));
    }

    public Task<CommandExecutionResult> InstallAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return CommandResultCapture.ExecuteAsync(() =>
            CreateCommand<SkillsInstallCommand>().InstallAsync(
                host: options.Host,
                scope: options.Scope,
                repoRoot: options.RepoRoot,
                targetDir: options.TargetDir,
                dryRun: options.DryRun,
                force: options.Force,
                printDiff: options.PrintDiff,
                tier: options.Tier,
                skill: options.Skill));
    }

    public Task<CommandExecutionResult> UpdateAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return CommandResultCapture.ExecuteAsync(() =>
            CreateCommand<SkillsUpdateCommand>().UpdateAsync(
                host: options.Host,
                scope: options.Scope,
                repoRoot: options.RepoRoot,
                targetDir: options.TargetDir,
                dryRun: options.DryRun,
                force: options.Force,
                printDiff: options.PrintDiff,
                tier: options.Tier,
                skill: options.Skill));
    }

    public Task<CommandExecutionResult> UninstallAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return CommandResultCapture.ExecuteAsync(() =>
            CreateCommand<SkillsUninstallCommand>().UninstallAsync(
                host: options.Host,
                scope: options.Scope,
                repoRoot: options.RepoRoot,
                targetDir: options.TargetDir,
                dryRun: options.DryRun,
                force: options.Force,
                tier: options.Tier,
                skill: options.Skill));
    }

    public Task<CommandExecutionResult> DoctorAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return CommandResultCapture.ExecuteAsync(() =>
            CreateCommand<SkillsDoctorCommand>().DoctorAsync(
                host: options.Host,
                scope: options.Scope,
                repoRoot: options.RepoRoot,
                targetDir: options.TargetDir,
                tier: options.Tier,
                skill: options.Skill));
    }

    private TCommand CreateCommand<TCommand> ()
        where TCommand : notnull
    {
        return ActivatorUtilities.CreateInstance<TCommand>(
            serviceProvider,
            CommandResultTestWriter.Create());
    }

    internal sealed record Options
    {
        public string? Host { get; init; }

        public string? Scope { get; init; }

        public string? RepoRoot { get; init; }

        public string? TargetDir { get; init; }

        public string? Output { get; init; }

        public string Format { get; init; } = "directory";

        public bool DryRun { get; init; }

        public bool Force { get; init; }

        public bool PrintDiff { get; init; }

        public string[]? Tier { get; init; }

        public string[]? Skill { get; init; }
    }
}
