namespace MackySoft.Ucli.Ops;

/// <summary> Provides the <c>ops</c> command workflow service. </summary>
internal interface IOpsService
{
    /// <summary> Executes <c>ops list</c>. </summary>
    /// <param name="input"> The raw command input values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized command result. </returns>
    ValueTask<OpsServiceResult<OpsListExecutionOutput>> List (
        OpsCommandInput input,
        CancellationToken cancellationToken = default);

    /// <summary> Executes <c>ops describe</c>. </summary>
    /// <param name="input"> The raw command input values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized command result. </returns>
    ValueTask<OpsServiceResult<OpsDescribeExecutionOutput>> Describe (
        OpsDescribeCommandInput input,
        CancellationToken cancellationToken = default);
}