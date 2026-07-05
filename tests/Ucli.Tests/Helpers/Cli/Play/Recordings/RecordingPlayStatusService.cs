using MackySoft.Ucli.Application.Features.Play.UseCases.Status;

namespace MackySoft.Tests;

internal sealed class RecordingPlayStatusService : RecordingCommandService<PlayStatusCommandInput, PlayStatusExecutionResult>, IPlayStatusService
{
    public RecordingPlayStatusService (Func<PlayStatusCommandInput, CancellationToken, ValueTask<PlayStatusExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<PlayStatusExecutionResult> ExecuteAsync (
        PlayStatusCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
