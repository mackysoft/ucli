using ConsoleAppFramework;
using MackySoft.Ucli.Features.OperationCatalog;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Provides the <c>ops describe</c> CLI command entry point. </summary>
internal sealed class OpsDescribeCommand
{
    private readonly IOpsService opsService;

    /// <summary> Initializes a new instance of the <see cref="OpsDescribeCommand" /> class. </summary>
    /// <param name="opsService"> The ops workflow service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="opsService" /> is <see langword="null" />. </exception>
    public OpsDescribeCommand (IOpsService opsService)
    {
        this.opsService = opsService ?? throw new ArgumentNullException(nameof(opsService));
    }

    /// <summary> Executes the <c>ops describe</c> command and emits the JSON result contract. </summary>
    /// <param name="operationName">The target operation name.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (<c>auto|daemon|oneshot</c>).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (<c>disabled|allowStale|requireFresh</c>).</param>
    /// <param name="failFast">--failFast, Fails immediately when live fallback hits a non-ready Unity editor lifecycle.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.DescribeSubcommand)]
    public async Task<int> Describe (
        [Argument]
        string operationName,
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var serviceResult = await opsService.Describe(
                new OpsDescribeCommandInput(
                    OperationName: operationName,
                    ProjectPath: projectPath,
                    Mode: mode,
                    Timeout: timeout,
                    ReadIndexMode: readIndexMode,
                    FailFast: failFast),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = OpsCommandResultFactory.CreateDescribe(serviceResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}