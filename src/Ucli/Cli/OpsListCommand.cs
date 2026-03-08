using ConsoleAppFramework;
using MackySoft.Ucli.Ops;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>ops list</c> CLI command entry point. </summary>
internal sealed class OpsListCommand
{
    private readonly IOpsService opsService;

    /// <summary> Initializes a new instance of the <see cref="OpsListCommand" /> class. </summary>
    /// <param name="opsService"> The ops workflow service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="opsService" /> is <see langword="null" />. </exception>
    public OpsListCommand (IOpsService opsService)
    {
        this.opsService = opsService ?? throw new ArgumentNullException(nameof(opsService));
    }

    /// <summary> Executes the <c>ops list</c> command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (<c>auto|daemon|oneshot</c>).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (<c>disabled|allowStale|requireFresh</c>).</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.ListSubcommand)]
    public async Task<int> List (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var serviceResult = await opsService.GetAll(
                new OpsCommandInput(
                    ProjectPath: projectPath,
                    Mode: mode,
                    Timeout: timeout,
                    ReadIndexMode: readIndexMode),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = OpsCommandResultFactory.CreateList(serviceResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}