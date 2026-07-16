namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Represents a Unity lifecycle readiness policy decision. </summary>
internal sealed class UnityReadinessDecision
{
    private static readonly UnityReadinessDecision ReadyDecision = new(
        isReady: true,
        errorCode: null,
        errorMessage: null);

    private static readonly UnityReadinessDecision WaitDecision = new(
        isReady: false,
        errorCode: null,
        errorMessage: null);

    private UnityReadinessDecision (
        bool isReady,
        UcliCode? errorCode,
        string? errorMessage)
    {
        IsReady = isReady;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary> Gets whether Unity is ready. </summary>
    public bool IsReady { get; }

    /// <summary> Gets whether readiness failed rather than remaining waitable. </summary>
    public bool IsFailure => ErrorCode is not null;

    /// <summary> Gets the failure code, or <see langword="null" /> for ready and wait decisions. </summary>
    public UcliCode? ErrorCode { get; }

    /// <summary> Gets the failure message, or <see langword="null" /> for ready and wait decisions. </summary>
    public string? ErrorMessage { get; }

    /// <summary> Creates a decision that indicates Unity is ready. </summary>
    public static UnityReadinessDecision Ready ()
    {
        return ReadyDecision;
    }

    /// <summary> Creates a decision that indicates readiness should keep waiting. </summary>
    public static UnityReadinessDecision Wait ()
    {
        return WaitDecision;
    }

    /// <summary> Creates a decision that indicates Unity readiness failed. </summary>
    public static UnityReadinessDecision Failure (
        UcliCode errorCode,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new UnityReadinessDecision(
            isReady: false,
            errorCode,
            errorMessage);
    }
}
