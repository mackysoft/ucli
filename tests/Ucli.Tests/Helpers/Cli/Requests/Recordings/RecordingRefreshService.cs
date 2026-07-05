using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

namespace MackySoft.Tests;

internal sealed class RecordingRefreshService : RecordingCommandService<RefreshCommandInput, OperationExecuteResult>, IRefreshService
{
    public RecordingRefreshService (Func<RefreshCommandInput, CancellationToken, ValueTask<OperationExecuteResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<OperationExecuteResult> ExecuteAsync (
        RefreshCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
