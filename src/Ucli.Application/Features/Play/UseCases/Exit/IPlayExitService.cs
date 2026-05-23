namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Provides Play Mode exit workflow execution. </summary>
internal interface IPlayExitService
{
    /// <summary> Executes one Play Mode exit workflow. </summary>
    /// <param name="input"> The normalized command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the Play Mode exit result. </returns>
    ValueTask<PlayExitExecutionResult> ExecuteAsync (
        PlayExitCommandInput input,
        CancellationToken cancellationToken = default);
}
