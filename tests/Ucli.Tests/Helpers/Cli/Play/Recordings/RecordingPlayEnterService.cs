using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

namespace MackySoft.Tests;

internal sealed class RecordingPlayEnterService : RecordingCommandService<PlayEnterCommandInput, PlayEnterExecutionResult>, IPlayEnterService
{
    public RecordingPlayEnterService (Func<PlayEnterCommandInput, CancellationToken, ValueTask<PlayEnterExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<PlayEnterExecutionResult> ExecuteAsync (
        PlayEnterCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }

    public ValueTask<PlayEnterExecutionResult> ReconnectAsync (
        PlayEnterCommandInput input,
        ExecutionRef lifecycleExecutionRef,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Play enter reconnect was not expected.");
    }
}
