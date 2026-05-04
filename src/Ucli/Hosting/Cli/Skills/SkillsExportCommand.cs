using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Hosts.Registration;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Provides the skills export CLI command entry point. </summary>
internal sealed class SkillsExportCommand
{
    private readonly OfficialSkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillExportService exportService;

    /// <summary> Initializes a new instance of the <see cref="SkillsExportCommand" /> class. </summary>
    /// <param name="packageProvider"> The official SKILL package provider. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="exportService"> The SKILL export service. </param>
    public SkillsExportCommand (
        OfficialSkillPackageProvider packageProvider,
        SkillHostAdapterSet hostAdapters,
        SkillExportService exportService)
    {
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
    }

    /// <summary> Executes the skills export command and emits the JSON result contract. </summary>
    /// <param name="host"> Required target host (claude|copilot|openai). </param>
    /// <param name="output"> Required output directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ExportSubcommand)]
    public async Task<int> Export (
        string? host = null,
        string? output = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var normalizedHost = SkillsCommandOptionNormalizer.NormalizeHost(
            UcliCommandNames.SkillsExport,
            host,
            hostAdapters,
            out var errorResult);
        if (errorResult is not null)
        {
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var outputRoot = SkillsCommandOptionNormalizer.NormalizeRequiredFullPath(
            UcliCommandNames.SkillsExport,
            "output",
            output,
            out errorResult);
        if (errorResult is not null)
        {
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var packagesResult = await packageProvider.GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        if (!packagesResult.IsSuccess)
        {
            var packageErrorResult = SkillsCommandResultFactory.CreateSkillFailure(UcliCommandNames.SkillsExport, packagesResult.Failure!);
            CommandResultWriter.WriteToStandardOutput(packageErrorResult);
            return packageErrorResult.ExitCode;
        }

        var exportResult = await exportService.ExportAsync(
                packagesResult.Value!,
                normalizedHost!,
                outputRoot!,
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = SkillsCommandResultFactory.CreateExport(exportResult, packagesResult.Value!, normalizedHost!);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
