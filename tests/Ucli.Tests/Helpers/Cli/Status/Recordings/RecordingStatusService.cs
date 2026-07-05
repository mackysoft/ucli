using MackySoft.Ucli.Application.Features.Status.UseCases.Status;

namespace MackySoft.Tests;

internal sealed class RecordingStatusService : RecordingCommandService<StatusCommandInput, StatusExecutionResult>, IStatusService
{
    public RecordingStatusService (Func<StatusCommandInput, CancellationToken, ValueTask<StatusExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<StatusExecutionResult> ExecuteAsync (
        StatusCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
