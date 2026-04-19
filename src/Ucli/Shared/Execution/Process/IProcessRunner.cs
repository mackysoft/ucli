namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Runs external processes with timeout and cancellation support. </summary>
internal interface IProcessRunner
{
    /// <summary> Runs one process request. </summary>
    /// <param name="request"> The process request values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by the caller. </param>
    /// <returns> A task that resolves to the process execution result. </returns>
    Task<ProcessRunResult> RunAsync (
        ProcessRunRequest request,
        CancellationToken cancellationToken = default);
}