namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one Unity request execution failure from the IPC boundary. </summary>
internal sealed record UnityRequestFailure
{
    /// <summary> Initializes a new instance of the <see cref="UnityRequestFailure" /> class. </summary>
    /// <param name="failureKind"> The machine-readable failure kind. </param>
    /// <param name="code"> The machine-readable failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="startupFailure"> The structured startup failure detail when Unity did not accept the request. </param>
    public UnityRequestFailure (
        UnityRequestFailureKind failureKind,
        UcliCode code,
        string message,
        StartupFailureDetail? startupFailure = null)
    {
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "Failure kind is invalid.");
        }

        if (!code.IsValid)
        {
            throw new ArgumentException("Failure code must not be empty.", nameof(code));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        FailureKind = failureKind;
        Code = code;
        Message = message;
        StartupFailure = startupFailure;
    }

    /// <summary> Gets the machine-readable failure kind. </summary>
    public UnityRequestFailureKind FailureKind { get; }

    /// <summary> Gets the machine-readable failure code. </summary>
    public UcliCode Code { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message { get; }

    /// <summary> Gets the structured startup failure detail when Unity did not accept the request. </summary>
    public StartupFailureDetail? StartupFailure { get; }
}
