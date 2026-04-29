using MackySoft.Ucli.Features.Status.Common.Contracts;

namespace MackySoft.Ucli.Features.Status.UseCases.Status;

/// <summary> Executes the status command workflow and produces one normalized status result. </summary>
internal interface IStatusService
{
    /// <summary> Executes status workflow using project, timeout, and daemon diagnostics contracts. </summary>
    /// <param name="input"> The normalized status command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the status execution result. </returns>
    ValueTask<StatusExecutionResult> Execute (
        StatusCommandInput input,
        CancellationToken cancellationToken = default);
}
