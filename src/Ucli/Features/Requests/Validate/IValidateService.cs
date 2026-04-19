namespace MackySoft.Ucli.Features.Requests.Validate;

/// <summary> Executes the <c>validate</c> command workflow. </summary>
internal interface IValidateService
{
    /// <summary> Executes one <c>validate</c> workflow and returns the normalized result. </summary>
    /// <param name="input"> The raw command input values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the normalized validate result. </returns>
    ValueTask<ValidateServiceResult> Execute (
        ValidateCommandInput input,
        CancellationToken cancellationToken = default);
}