using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

namespace MackySoft.Tests;

internal sealed class RecordingRefreshService : RecordingCommandService<RefreshCommandInput, OperationExecuteResult>, IRefreshService
{
    private readonly List<Guid> requestIds = [];

    public RecordingRefreshService (Func<RefreshCommandInput, CancellationToken, ValueTask<OperationExecuteResult>> handler)
        : base(handler)
    {
    }

    public IReadOnlyList<Guid> RequestIds => requestIds;

    public ValueTask<OperationExecuteResult> ExecuteAsync (
        Guid requestId,
        RefreshCommandInput input,
        CancellationToken cancellationToken = default)
    {
        requestIds.Add(requestId);
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
