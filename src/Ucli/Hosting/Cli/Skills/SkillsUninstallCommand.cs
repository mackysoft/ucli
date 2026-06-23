using ConsoleAppFramework;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Services;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Provides the skills uninstall CLI command entry point. </summary>
internal sealed class SkillsUninstallCommand
{
    private readonly SkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillUninstallService uninstallService;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="SkillsUninstallCommand" /> class. </summary>
    /// <param name="packageProvider"> The official SKILL package provider. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="uninstallService"> The SKILL uninstall service. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public SkillsUninstallCommand (
        SkillPackageProvider packageProvider,
        SkillHostAdapterSet hostAdapters,
        SkillUninstallService uninstallService,
        ICommandResultWriter commandResultWriter)
    {
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.uninstallService = uninstallService ?? throw new ArgumentNullException(nameof(uninstallService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the skills uninstall command and emits the JSON result contract. </summary>
    /// <param name="host"> Required target host (claude|copilot|openai). </param>
    /// <param name="scope"> Required install scope (project|user). </param>
    /// <param name="repoRoot"> --repoRoot, Optional repository root override for project scope. </param>
    /// <param name="targetDir"> --targetDir, Optional target root path under the repository root. </param>
    /// <param name="dryRun"> --dryRun, Whether to return the uninstall plan without writing. </param>
    /// <param name="force"> Whether managed local modifications can be deleted. </param>
    /// <param name="tier"> Optional SKILL tier literals. Required when <paramref name="skill" /> is omitted. </param>
    /// <param name="skill"> Optional exact SKILL name literals. Required when <paramref name="tier" /> is omitted. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.UninstallSubcommand)]
    public async Task<int> UninstallAsync (
        string? host = null,
        string? scope = null,
        string? repoRoot = null,
        string? targetDir = null,
        bool dryRun = false,
        bool force = false,
        string[]? tier = null,
        string[]? skill = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedHost = SkillsCommandOptionNormalizer.NormalizeHost(
            UcliCommandNames.SkillsUninstall,
            host,
            hostAdapters,
            out var errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedScope = SkillsCommandOptionNormalizer.NormalizeScope(
            UcliCommandNames.SkillsUninstall,
            scope,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var repositoryRoot = SkillsCommandOptionNormalizer.NormalizeRepositoryRootForScope(
            UcliCommandNames.SkillsUninstall,
            normalizedScope!.Value,
            repoRoot,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var selection = SkillsCommandOptionNormalizer.NormalizeRequiredPackageSelection(
            UcliCommandNames.SkillsUninstall,
            tier,
            skill,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var packagesResult = await packageProvider.GetPackagesAsync(UcliSkillTierLiterals.Defined, selection!.Tiers, selection.SkillNames, cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            var packageErrorResult = SkillsCommandResultFactory.CreateSkillFailure(UcliCommandNames.SkillsUninstall, packagesResult.Failure!);
            commandResultWriter.WriteToStandardOutput(packageErrorResult);
            return packageErrorResult.ExitCode;
        }

        var uninstallResult = await uninstallService.UninstallAsync(
                new SkillUninstallInput(
                    packagesResult.Value!,
                    new SkillInstallRequest(normalizedHost!, normalizedScope!.Value, repositoryRoot!, targetDir),
                    dryRun,
                    force),
                cancellationToken)
            .ConfigureAwait(false);
        var reloadGuidance = hostAdapters.GetAdapter(normalizedHost!).Value!.Descriptor.ReloadGuidance;
        var commandResult = SkillsCommandResultFactory.CreateUninstall(
            uninstallResult,
            normalizedHost!,
            normalizedScope.Value,
            repositoryRoot,
            reloadGuidance,
            selection.Tiers.Select(static tier => tier.Value).ToArray(),
            selection.SkillNames);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
