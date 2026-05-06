using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Represents one request-service failure converted from a lower execution boundary. </summary>
internal sealed record RequestServiceFailure
{
    /// <summary> Initializes a new instance of the <see cref="RequestServiceFailure" /> class. </summary>
    /// <param name="error"> The operation-level failure error. </param>
    /// <param name="outcome"> The application-level failure outcome. </param>
    public RequestServiceFailure (
        OperationExecutionError error,
        ApplicationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (outcome == ApplicationOutcome.Success)
        {
            throw new ArgumentException("Failure outcome must not be success.", nameof(outcome));
        }

        Error = error;
        Outcome = outcome;
    }

    /// <summary> Gets the operation-level failure error. </summary>
    public OperationExecutionError Error { get; }

    /// <summary> Gets the application-level failure outcome. </summary>
    public ApplicationOutcome Outcome { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message => Error.Message;
}
