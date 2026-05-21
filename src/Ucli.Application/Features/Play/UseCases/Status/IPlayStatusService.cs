namespace MackySoft.Ucli.Application.Features.Play.UseCases.Status;

/// <summary> Provides Play Mode status observation workflow execution. </summary>
internal interface IPlayStatusService
{
    /// <summary> Executes one Play Mode status observation workflow. </summary>
    /// <param name="input"> The normalized command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the Play Mode status result. </returns>
    ValueTask<PlayStatusExecutionResult> ExecuteAsync (
        PlayStatusCommandInput input,
        CancellationToken cancellationToken = default);
}
