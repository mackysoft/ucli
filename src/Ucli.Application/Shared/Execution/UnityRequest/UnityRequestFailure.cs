namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one classified Unity request execution failure. </summary>
internal sealed record UnityRequestFailure
{
    /// <summary> Initializes a new instance of the <see cref="UnityRequestFailure" /> class. </summary>
    /// <param name="code"> The machine-readable failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="outcome"> The application-level outcome represented by this failure. </param>
    public UnityRequestFailure (
        string code,
        string message,
        ApplicationOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (outcome == ApplicationOutcome.Success)
        {
            throw new ArgumentException("Failure outcome must not be success.", nameof(outcome));
        }

        var resolvedOutcome = ApplicationFailureOutcomeResolver.Resolve(code);
        if (outcome != resolvedOutcome)
        {
            throw new ArgumentException("Failure outcome must match the failure code.", nameof(outcome));
        }

        Code = code;
        Message = message;
        Outcome = outcome;
    }

    /// <summary> Gets the machine-readable failure code. </summary>
    public string Code { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message { get; }

    /// <summary> Gets the application-level outcome represented by this failure. </summary>
    public ApplicationOutcome Outcome { get; }
}
