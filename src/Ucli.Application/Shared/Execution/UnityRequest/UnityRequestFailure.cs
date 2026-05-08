namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one Unity request execution failure from the IPC boundary. </summary>
internal sealed record UnityRequestFailure
{
    /// <summary> Initializes a new instance of the <see cref="UnityRequestFailure" /> class. </summary>
    /// <param name="code"> The machine-readable failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    public UnityRequestFailure (
        UcliErrorCode code,
        string message)
    {
        if (!code.IsValid)
        {
            throw new ArgumentException("Failure code must not be empty.", nameof(code));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
    }

    /// <summary> Gets the machine-readable failure code. </summary>
    public UcliErrorCode Code { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message { get; }
}
