namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;

/// <summary> Resolves command input and read-index prerequisites before ops catalog access. </summary>
internal interface IOpsPreflightService
{
    /// <summary> Executes one preflight flow and returns either resolved context or failure output. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to preflight result values. </returns>
    ValueTask<OpsPreflightResult> ExecuteAsync (
        OpsPreflightInput input,
        CancellationToken cancellationToken = default);
}
