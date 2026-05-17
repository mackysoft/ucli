namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Represents a daemon readiness policy decision. </summary>
internal readonly record struct UnityDaemonReadinessDecision (
    bool IsReady,
    bool IsFailure,
    UcliCodeValue? ErrorCode,
    string? ErrorMessage)
{
    /// <summary> Creates a decision that indicates daemon is ready. </summary>
    /// <returns> The ready decision. </returns>
    public static UnityDaemonReadinessDecision Ready ()
    {
        return new UnityDaemonReadinessDecision(
            IsReady: true,
            IsFailure: false,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary> Creates a decision that indicates readiness should keep waiting. </summary>
    /// <returns> The wait decision. </returns>
    public static UnityDaemonReadinessDecision Wait ()
    {
        return new UnityDaemonReadinessDecision(
            IsReady: false,
            IsFailure: false,
            ErrorCode: null,
            ErrorMessage: null);
    }

    /// <summary> Creates a decision that indicates daemon readiness failed. </summary>
    /// <param name="errorCode"> The machine-readable error code. </param>
    /// <param name="errorMessage"> The user-facing error message. </param>
    /// <returns> The failure decision. </returns>
    public static UnityDaemonReadinessDecision Failure (
        UcliCodeValue errorCode,
        string errorMessage)
    {
        if (!errorCode.IsValid)
        {
            throw new ArgumentException("Error code must not be empty.", nameof(errorCode));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new UnityDaemonReadinessDecision(
            IsReady: false,
            IsFailure: true,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }
}
