namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Executes the <c>ready</c> assurance workflow. </summary>
internal interface IReadyService
{
    /// <summary> Executes one ready workflow and returns an assurance packet. </summary>
    ValueTask<ReadyExecutionResult> ExecuteAsync (
        ReadyCommandInput input,
        CancellationToken cancellationToken = default);
}
