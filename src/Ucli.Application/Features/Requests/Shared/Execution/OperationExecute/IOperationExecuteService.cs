namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Executes one fixed operation through the shared CLI request pipeline. </summary>
internal interface IOperationExecuteService
{
    /// <summary> Executes the specified fixed operation and returns the normalized execution result. </summary>
    /// <param name="definition"> The fixed operation definition to execute. </param>
    /// <param name="input"> The normalized execution input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized operation execution result. </returns>
    ValueTask<OperationExecuteResult> Execute (
        OperationExecuteDefinition definition,
        OperationExecuteInput input,
        CancellationToken cancellationToken = default);
}
