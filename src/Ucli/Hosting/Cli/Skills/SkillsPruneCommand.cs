using ConsoleAppFramework;
using MackySoft.AgentSkills.Catalogs;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.AgentSkills.Installation.Requests;
using MackySoft.AgentSkills.Installation.Services;
using MackySoft.AgentSkills.Installation.Targeting;
using MackySoft.AgentSkills.Names;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Provides the skills prune CLI command entry point. </summary>
internal sealed class SkillsPruneCommand
{
    private readonly SkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillPruneService pruneService;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="SkillsPruneCommand" /> class. </summary>
    /// <param name="packageProvider"> The official SKILL package provider. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="pruneService"> The SKILL prune service. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public SkillsPruneCommand (
        SkillPackageProvider packageProvider,
        SkillHostAdapterSet hostAdapters,
        SkillPruneService pruneService,
        ICommandResultWriter commandResultWriter)
    {
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.pruneService = pruneService ?? throw new ArgumentNullException(nameof(pruneService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the skills prune command and emits the JSON result contract. </summary>
    /// <param name="host"> Required target host (claude|copilot|openai). </param>
    /// <param name="scope"> Required install scope (project|user). </param>
    /// <param name="repoRoot"> --repoRoot, Optional repository root override for project scope. </param>
    /// <param name="targetDir"> --targetDir, Optional target root path under the repository root. </param>
    /// <param name="dryRun"> --dryRun, Whether to return the prune plan without writing. </param>
    /// <param name="force"> Whether managed local modifications can be deleted. </param>
    /// <param name="tier"> Optional SKILL tier literals. Required when <paramref name="skill" /> is omitted. </param>
    /// <param name="skill"> Optional exact SKILL name literals. Required when <paramref name="tier" /> is omitted. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.PruneSubcommand)]
    public async Task<int> PruneAsync (
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
            UcliCommandNames.SkillsPrune,
            host,
            hostAdapters,
            out var errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedScope = SkillsCommandOptionNormalizer.NormalizeScope(
            UcliCommandNames.SkillsPrune,
            scope,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var repositoryRoot = SkillsCommandOptionNormalizer.NormalizeRepositoryRootForScope(
            UcliCommandNames.SkillsPrune,
            normalizedScope!.Value,
            repoRoot,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var selection = SkillsCommandOptionNormalizer.NormalizeRequiredPruneSelection(
            UcliCommandNames.SkillsPrune,
            tier,
            skill,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var catalogResult = await packageProvider.GetPackageCatalogAsync(UcliSkillTierLiterals.Defined, cancellationToken).ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            var packageErrorResult = SkillsCommandResultFactory.CreateSkillFailure(UcliCommandNames.SkillsPrune, catalogResult.Failure!);
            commandResultWriter.WriteToStandardOutput(packageErrorResult);
            return packageErrorResult.ExitCode;
        }

        var pruneResult = await pruneService.PruneAsync(
                new SkillPruneInput(
                    new SkillCatalogId(UcliSkillCatalogLiterals.Official),
                    catalogResult.Value!.Packages,
                    new SkillInstallRequest(normalizedHost!, normalizedScope!.Value, repositoryRoot, targetDir),
                    dryRun,
                    force,
                    selection!.TierFilter,
                    selection.SkillNames.Select(static skillName => new SkillName(skillName)).ToArray()),
                cancellationToken)
            .ConfigureAwait(false);
        var reloadGuidance = hostAdapters.GetAdapter(normalizedHost!).Value!.Descriptor.ReloadGuidance;
        var commandResult = SkillsCommandResultFactory.CreatePrune(
            pruneResult,
            normalizedHost!,
            normalizedScope.Value,
            repositoryRoot,
            reloadGuidance,
            selection.ReportTiers.Select(static tier => tier.Value).ToArray(),
            selection.SkillNames);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
