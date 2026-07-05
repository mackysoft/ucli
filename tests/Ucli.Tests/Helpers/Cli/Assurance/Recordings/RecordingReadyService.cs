using MackySoft.Ucli.Application.Features.Assurance.Ready;

namespace MackySoft.Tests;

internal sealed class RecordingReadyService : RecordingCommandService<ReadyCommandInput, ReadyExecutionResult>, IReadyService
{
    public RecordingReadyService (Func<ReadyCommandInput, CancellationToken, ValueTask<ReadyExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<ReadyExecutionResult> ExecuteAsync (
        ReadyCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
