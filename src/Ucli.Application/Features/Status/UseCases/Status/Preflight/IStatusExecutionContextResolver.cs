namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Preflight;

/// <summary> Resolves preflight execution context values required by the status workflow. </summary>
internal interface IStatusExecutionContextResolver
{
    /// <summary> Resolves context, timeout, and Unity version values for one status execution. </summary>
    /// <param name="input"> The normalized status command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the execution-context resolution result. </returns>
    ValueTask<StatusExecutionContextResolutionResult> Resolve (
        StatusCommandInput input,
        CancellationToken cancellationToken = default);
}
