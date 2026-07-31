using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

namespace MackySoft.Tests;

internal sealed class RecordingRefreshService : RecordingCommandService<RefreshCommandInput, RefreshExecutionResult>, IRefreshService
{
    private readonly List<Guid> requestIds = [];

    public RecordingRefreshService (Func<RefreshCommandInput, CancellationToken, ValueTask<RefreshExecutionResult>> handler)
        : base(handler)
    {
    }

    public IReadOnlyList<Guid> RequestIds => requestIds;

    public ValueTask<RefreshExecutionResult> ExecuteAsync (
        Guid requestId,
        RefreshCommandInput input,
        CancellationToken cancellationToken = default)
    {
        requestIds.Add(requestId);
        return ExecuteRecordedAsync(input, cancellationToken);
    }

    public ValueTask<RefreshExecutionResult> ReconnectAsync (
        Guid requestId,
        RefreshCommandInput input,
        ExecutionRef lifecycleExecutionRef,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Refresh reconnect was not expected.");
    }
}
