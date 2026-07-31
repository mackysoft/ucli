using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Represents the result of Play Mode enter workflow execution. </summary>
internal sealed record PlayEnterExecutionResult (
    PlayEnterExecutionOutput? Output,
    ApplicationFailure? Error,
    PlayTransitionFailureContext? FailureContext)
{
    private const string SuccessMessage = "uCLI play enter completed.";

    private const string FailureMessage = "uCLI play enter failed.";

    /// <summary> Gets a value indicating whether the workflow succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Gets the user-facing command message. </summary>
    public string Message => IsSuccess ? SuccessMessage : Error?.Message ?? FailureMessage;

    /// <summary> Creates a successful result. </summary>
    /// <param name="output"> The normalized output payload values. </param>
    /// <returns> The successful result. </returns>
    public static PlayEnterExecutionResult Success (PlayEnterExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.LifecycleExecutionRef.State.Value
            != TextVocabulary.GetText(LifecycleExecutionState.Completed))
        {
            throw new ArgumentException(
                "Successful Play Mode enter result requires a completed terminal Lifecycle Execution reference.",
                nameof(output));
        }

        return new PlayEnterExecutionResult(output, null, null);
    }

    /// <summary> Creates a failed result from a structured execution error. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed result. </returns>
    public static PlayEnterExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failure(ApplicationFailure.FromExecutionError(error));
    }

    /// <summary> Creates a failed result from an application failure. </summary>
    /// <param name="failure"> The classified application failure. </param>
    /// <param name="failureContext"> The durable identity and any typed transition facts established before failure. </param>
    /// <returns> The failed result. </returns>
    public static PlayEnterExecutionResult Failure (
        ApplicationFailure failure,
        PlayTransitionFailureContext? failureContext = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new PlayEnterExecutionResult(
            Output: null,
            failure,
            failureContext);
    }
}
