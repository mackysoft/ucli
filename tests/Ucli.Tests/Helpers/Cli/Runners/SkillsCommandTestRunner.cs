using MackySoft.AgentSkills.Hosting.Commands;
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
        return ExecuteRunnerAsync(runner => runner.ListAsync(new AgentSkillsListCommandRequest(tier, skill)));
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
            UcliCommandNames.PruneSubcommand => PruneAsync(options),
            UcliCommandNames.DoctorSubcommand => DoctorAsync(options),
            _ => throw new ArgumentOutOfRangeException(nameof(subcommand), subcommand, "Unsupported skills subcommand."),
        };
    }

    public Task<CommandExecutionResult> ExportAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteRunnerAsync(runner => runner.ExportAsync(new AgentSkillsExportCommandRequest(
            options.Host,
            options.Tier,
            options.Skill,
            options.Output,
            options.Format)));
    }

    public Task<CommandExecutionResult> InstallAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteRunnerAsync(runner => runner.InstallAsync(new AgentSkillsInstallCommandRequest(
            options.Host,
            options.Tier,
            options.Skill,
            options.Scope,
            options.RepoRoot,
            options.TargetDir,
            options.DryRun,
            options.Force,
            options.PrintDiff)));
    }

    public Task<CommandExecutionResult> UpdateAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteRunnerAsync(runner => runner.UpdateAsync(new AgentSkillsUpdateCommandRequest(
            options.Host,
            options.Tier,
            options.Skill,
            options.Scope,
            options.RepoRoot,
            options.TargetDir,
            options.DryRun,
            options.Force,
            options.PrintDiff)));
    }

    public Task<CommandExecutionResult> UninstallAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteRunnerAsync(runner => runner.UninstallAsync(new AgentSkillsUninstallCommandRequest(
            options.Host,
            options.Tier,
            options.Skill,
            options.Scope,
            options.RepoRoot,
            options.TargetDir,
            options.DryRun,
            options.Force)));
    }

    public Task<CommandExecutionResult> PruneAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteRunnerAsync(runner => runner.PruneAsync(new AgentSkillsPruneCommandRequest(
            options.Host,
            options.Tier,
            options.Skill,
            options.Scope,
            options.RepoRoot,
            options.TargetDir,
            options.DryRun,
            options.Force)));
    }

    public Task<CommandExecutionResult> DoctorAsync (Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ExecuteRunnerAsync(runner => runner.DoctorAsync(new AgentSkillsDoctorCommandRequest(
            options.Host,
            options.Tier,
            options.Skill,
            options.Scope,
            options.RepoRoot,
            options.TargetDir)));
    }

    private Task<CommandExecutionResult> ExecuteRunnerAsync (
        Func<AgentSkillsCommandRunner, ValueTask<AgentSkillsCommandResult>> executeAsync)
    {
        ArgumentNullException.ThrowIfNull(executeAsync);

        return CommandResultCapture.ExecuteAsync(async () =>
        {
            var runner = serviceProvider.GetRequiredService<AgentSkillsCommandRunner>();
            var emitter = ActivatorUtilities.CreateInstance<UcliAgentSkillsCommandResultEmitter>(
                serviceProvider,
                CommandResultTestWriter.Create());
            var result = await executeAsync(runner).ConfigureAwait(false);
            return await emitter.EmitAsync(result, new AgentSkillsCommandOutputOptions(), CancellationToken.None).ConfigureAwait(false);
        });
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
