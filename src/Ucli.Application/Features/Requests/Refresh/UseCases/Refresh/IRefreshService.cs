using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary> Executes the <c>refresh</c> command workflow. </summary>
internal interface IRefreshService
{
    /// <summary> Executes one <c>refresh</c> workflow and returns the normalized execution result. </summary>
    /// <param name="input"> The normalized command input values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the refresh execution result. </returns>
    ValueTask<OperationExecuteResult> ExecuteAsync (
        RefreshCommandInput input,
        CancellationToken cancellationToken = default);
}
