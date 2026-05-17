namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Represents a Unity lifecycle readiness policy decision. </summary>
internal readonly record struct UnityReadinessDecision (
    bool IsReady,
    bool IsFailure,
    UcliCode? ErrorCode,
    string? ErrorMessage)
{
    /// <summary> Creates a decision that indicates Unity is ready. </summary>
    public static UnityReadinessDecision Ready ()
    {
        return new UnityReadinessDecision(
            IsReady: true,
            IsFailure: false,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary> Creates a decision that indicates readiness should keep waiting. </summary>
    public static UnityReadinessDecision Wait ()
    {
        return new UnityReadinessDecision(
            IsReady: false,
            IsFailure: false,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary> Creates a decision that indicates Unity readiness failed. </summary>
    public static UnityReadinessDecision Failure (
        UcliCode errorCode,
        string errorMessage)
    {
        if (!errorCode.IsValid)
        {
            throw new ArgumentException("Error code must not be empty.", nameof(errorCode));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new UnityReadinessDecision(
            IsReady: false,
            IsFailure: true,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }
}
