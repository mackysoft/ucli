using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Provides the skills update CLI command entry point. </summary>
internal sealed class SkillsUpdateCommand
{
    private readonly OfficialSkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillUpdateService updateService;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="SkillsUpdateCommand" /> class. </summary>
    /// <param name="packageProvider"> The official SKILL package provider. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="updateService"> The SKILL update service. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public SkillsUpdateCommand (
        OfficialSkillPackageProvider packageProvider,
        SkillHostAdapterSet hostAdapters,
        SkillUpdateService updateService,
        ICommandResultWriter commandResultWriter)
    {
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the skills update command and emits the JSON result contract. </summary>
    /// <param name="host"> Required target host (claude|copilot|openai). </param>
    /// <param name="scope"> Required install scope (project|user). </param>
    /// <param name="repoRoot"> --repoRoot, Required repository root for project scope. </param>
    /// <param name="targetDir"> --targetDir, Optional target root path under the repository root. </param>
    /// <param name="dryRun"> --dryRun, Whether to return the update plan without writing. </param>
    /// <param name="force"> Whether managed local modifications can be overwritten. </param>
    /// <param name="printDiff"> --printDiff, Whether structured file diffs should be included. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.UpdateSubcommand)]
    public async Task<int> Update (
        string? host = null,
        string? scope = null,
        string? repoRoot = null,
        string? targetDir = null,
        bool dryRun = false,
        bool force = false,
        bool printDiff = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedHost = SkillsCommandOptionNormalizer.NormalizeHost(
            UcliCommandNames.SkillsUpdate,
            host,
            hostAdapters,
            out var errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedScope = SkillsCommandOptionNormalizer.NormalizeScope(
            UcliCommandNames.SkillsUpdate,
            scope,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var repositoryRoot = SkillsCommandOptionNormalizer.NormalizeRepositoryRootForScope(
            UcliCommandNames.SkillsUpdate,
            normalizedScope!.Value,
            repoRoot,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var packagesResult = await packageProvider.GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            var packageErrorResult = SkillsCommandResultFactory.CreateSkillFailure(UcliCommandNames.SkillsUpdate, packagesResult.Failure!);
            commandResultWriter.WriteToStandardOutput(packageErrorResult);
            return packageErrorResult.ExitCode;
        }

        var updateResult = await updateService.UpdateAsync(
                new SkillUpdateInput(
                    packagesResult.Value!,
                    new SkillInstallRequest(normalizedHost!, normalizedScope!.Value, repositoryRoot!, targetDir),
                    dryRun,
                    force,
                    printDiff),
                cancellationToken)
            .ConfigureAwait(false);
        var reloadGuidance = hostAdapters.GetAdapter(normalizedHost!).Value!.Descriptor.ReloadGuidance;
        var commandResult = SkillsCommandResultFactory.CreateUpdate(updateResult, normalizedHost!, normalizedScope.Value, repositoryRoot, reloadGuidance);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
