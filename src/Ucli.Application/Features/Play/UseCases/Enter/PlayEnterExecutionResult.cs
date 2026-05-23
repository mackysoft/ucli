using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Represents the result of Play Mode enter workflow execution. </summary>
internal sealed record PlayEnterExecutionResult (
    PlayEnterExecutionOutput? Output,
    ApplicationFailure? Error)
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
        return new PlayEnterExecutionResult(output, null);
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
    /// <param name="output"> The transition output when Unity returned one before failing. </param>
    /// <returns> The failed result. </returns>
    public static PlayEnterExecutionResult Failure (
        ApplicationFailure failure,
        PlayEnterExecutionOutput? output = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new PlayEnterExecutionResult(output, failure);
    }
}
