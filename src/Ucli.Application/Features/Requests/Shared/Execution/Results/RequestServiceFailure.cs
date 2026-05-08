using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Represents one request-service failure converted from a lower execution boundary. </summary>
internal sealed record RequestServiceFailure
{
    /// <summary> Initializes a new instance of the <see cref="RequestServiceFailure" /> class. </summary>
    /// <param name="error"> The application failure. </param>
    public RequestServiceFailure (ApplicationFailure error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Error = error;
    }

    /// <summary> Gets the application failure. </summary>
    public ApplicationFailure Error { get; }

    /// <summary> Gets the application-level failure outcome. </summary>
    public ApplicationOutcome Outcome => Error.Outcome;

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message => Error.Message;
}
