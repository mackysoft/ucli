namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;

/// <summary> Executes verify assurance profiles and returns claim packets. </summary>
internal interface IVerifyService
{
    /// <summary> Executes one verify profile. </summary>
    /// <param name="input"> The normalized verify command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The verify execution result. </returns>
    ValueTask<VerifyExecutionResult> ExecuteAsync (
        VerifyCommandInput input,
        CancellationToken cancellationToken = default);
}
