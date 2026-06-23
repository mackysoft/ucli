using ConsoleAppFramework;
using MackySoft.AgentSkills.Distribution;
using MackySoft.AgentSkills.Hosts.Registration;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Provides the skills list CLI command entry point. </summary>
internal sealed class SkillsListCommand
{
    private readonly SkillPackageProvider packageProvider;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the <see cref="SkillsListCommand" /> class. </summary>
    /// <param name="packageProvider"> The official SKILL package provider. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public SkillsListCommand (
        SkillPackageProvider packageProvider,
        SkillHostAdapterSet hostAdapters,
        ICommandResultWriter commandResultWriter)
    {
        this.packageProvider = packageProvider ?? throw new ArgumentNullException(nameof(packageProvider));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the skills list command and emits the JSON result contract. </summary>
    /// <param name="tier"> Optional SKILL tier literals. Omit to list all defined tiers. </param>
    /// <param name="skill"> Optional exact SKILL name literals. Omit to list all selected tiers. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ListSubcommand)]
    public async Task<int> ListAsync (
        string[]? tier = null,
        string[]? skill = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var selection = SkillsCommandOptionNormalizer.NormalizeOptionalPackageSelection(
            UcliCommandNames.SkillsList,
            tier,
            skill,
            out var errorResult);
        if (errorResult is not null)
        {
            commandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var catalogResult = await packageProvider.GetPackageCatalogAsync(UcliSkillTierLiterals.Defined, selection!.Tiers, selection.SkillNames, cancellationToken).ConfigureAwait(false);
        var commandResult = catalogResult.IsSuccess
            ? SkillsCommandResultFactory.CreateList(
                catalogResult.Value!,
                hostAdapters)
            : SkillsCommandResultFactory.CreateSkillFailure(UcliCommandNames.SkillsList, catalogResult.Failure!);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
