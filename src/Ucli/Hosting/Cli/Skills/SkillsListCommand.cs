using ConsoleAppFramework;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Skills.Distribution;
using MackySoft.Ucli.Skills.Hosts.Registration;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Provides the skills list CLI command entry point. </summary>
internal sealed class SkillsListCommand
{
    private readonly OfficialSkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;

    /// <summary> Initializes a new instance of the <see cref="SkillsListCommand" /> class. </summary>
    /// <param name="packageProvider"> The official SKILL package provider. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    public SkillsListCommand (
        OfficialSkillPackageProvider packageProvider,
        SkillHostAdapterSet hostAdapters)
    {
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
    }

    /// <summary> Executes the skills list command and emits the JSON result contract. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ListSubcommand)]
    public async Task<int> List (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var packagesResult = await packageProvider.GetPackagesAsync(cancellationToken).ConfigureAwait(false);
        var commandResult = packagesResult.IsSuccess
            ? SkillsCommandResultFactory.CreateList(packagesResult.Value!, hostAdapters)
            : SkillsCommandResultFactory.CreateSkillFailure(UcliCommandNames.SkillsList, packagesResult.Failure!);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
