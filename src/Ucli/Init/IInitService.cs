namespace MackySoft.Ucli.Init;

/// <summary> Executes initialization flow for generating <c>.ucli</c> template files under the storage root resolved from the current working directory. </summary>
internal interface IInitService
{
    /// <summary> Executes initialization for the storage root resolved from the current working directory. </summary>
    /// <param name="force"> Whether existing files can be overwritten. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the initialization execution result that contains either generated file paths or a structured error. </returns>
    ValueTask<InitExecutionResult> Execute (
        bool force,
        CancellationToken cancellationToken = default);
}