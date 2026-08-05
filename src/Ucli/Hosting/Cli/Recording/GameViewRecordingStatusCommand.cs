using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Recording.UseCases;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Recording;

/// <summary>Provides the <c>recording status</c> CLI entry point.</summary>
internal sealed class GameViewRecordingStatusCommand
{
    private readonly IGameViewRecordingService recordingService;
    private readonly ICommandResultWriter commandResultWriter;

    public GameViewRecordingStatusCommand (
        IGameViewRecordingService recordingService,
        ICommandResultWriter commandResultWriter)
    {
        this.recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary>Reports recording capability and an optional durable recording selection.</summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="recordingId">--recordingId, Optional non-zero UUID selecting one recording.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="cancellationToken">The cancellation token propagated by command execution.</param>
    /// <returns>The exit code contained in the emitted command result.</returns>
    [Command(UcliCommandNames.Status)]
    public async Task<int> StatusAsync (
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        [OptionalRecordingIdArgumentParser] Guid? recordingId = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            return WriteError(timeoutResult.Error!);
        }

        var serviceResult = await recordingService.GetStatusAsync(
                new GameViewRecordingStatusInput(
                    ProjectPath: projectPath,
                    RecordingId: recordingId,
                    TimeoutMilliseconds: timeoutResult.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = GameViewRecordingCommandResultFactory.CreateStatus(serviceResult);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }

    private int WriteError (ExecutionError error)
    {
        var result = GameViewRecordingCommandResultFactory.CreateExecutionError(
            UcliCommandNames.RecordingStatus,
            error);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}
