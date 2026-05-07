using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Installation;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Provides the skills uninstall CLI command entry point. </summary>
internal sealed class SkillsUninstallCommand
{
    private readonly OfficialSkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillUninstallService uninstallService;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="SkillsUninstallCommand" /> class. </summary>
    /// <param name="packageProvider"> The official SKILL package provider. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="uninstallService"> The SKILL uninstall service. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public SkillsUninstallCommand (
        OfficialSkillPackageProvider packageProvider,
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
    /// <param name="scope"> Required install scope. Only project is supported. </param>
    /// <param name="repoRoot"> --repoRoot, Required repository root. </param>
    /// <param name="targetDir"> --targetDir, Optional target root path under the repository root. </param>
    /// <param name="dryRun"> --dryRun, Whether to return the uninstall plan without writing. </param>
    /// <param name="force"> Whether managed local modifications can be deleted. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.UninstallSubcommand)]
    public async Task<int> Uninstall (
        string? host = null,
        string? scope = null,
        string? repoRoot = null,
        string? targetDir = null,
        bool dryRun = false,
        bool force = false,
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

        var normalizedScope = SkillsCommandOptionNormalizer.NormalizeProjectScope(
            UcliCommandNames.SkillsUninstall,
            scope,
            out errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var repositoryRoot = SkillsCommandOptionNormalizer.NormalizeRequiredFullPath(
            UcliCommandNames.SkillsUninstall,
            "repoRoot",
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
        var commandResult = SkillsCommandResultFactory.CreateUninstall(uninstallResult, normalizedHost!, repositoryRoot!);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
