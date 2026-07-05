using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

namespace MackySoft.Tests;

internal sealed class RecordingPlayExitService : RecordingCommandService<PlayExitCommandInput, PlayExitExecutionResult>, IPlayExitService
{
    public RecordingPlayExitService (Func<PlayExitCommandInput, CancellationToken, ValueTask<PlayExitExecutionResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<PlayExitExecutionResult> ExecuteAsync (
        PlayExitCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
