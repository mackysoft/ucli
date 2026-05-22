namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Provides Play Mode enter workflow execution. </summary>
internal interface IPlayEnterService
{
    /// <summary> Executes one Play Mode enter workflow. </summary>
    /// <param name="input"> The normalized command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the Play Mode enter result. </returns>
    ValueTask<PlayEnterExecutionResult> ExecuteAsync (
        PlayEnterCommandInput input,
        CancellationToken cancellationToken = default);
}
