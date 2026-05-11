namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Represents one Unity test-execution result. </summary>
/// <param name="ProcessExitCode"> The Unity process exit code on success; otherwise <see langword="null" />. </param>
/// <param name="FailureKind"> The execution failure kind on failure; otherwise <see langword="null" />. </param>
/// <param name="ErrorMessage"> The user-facing execution error message on failure; otherwise <see langword="null" />. </param>
/// <param name="ErrorCode"> The machine-readable execution error code on failure; otherwise <see langword="null" />. </param>
/// <param name="StartupFailure"> The structured startup failure detail when Unity did not reach test execution. </param>
internal sealed record UnityTestExecutionResult (
    int? ProcessExitCode,
    UnityTestExecutionFailureKind? FailureKind,
    string? ErrorMessage,
    UcliErrorCode? ErrorCode,
    StartupFailureDetail? StartupFailure = null)
{
    /// <summary> Gets a value indicating whether execution succeeded. </summary>
    public bool IsSuccess => ProcessExitCode.HasValue && FailureKind is null;

    /// <summary> Creates a successful execution result. </summary>
    /// <param name="processExitCode"> The Unity process exit code. </param>
    /// <returns> The successful execution result. </returns>
    public static UnityTestExecutionResult Success (int processExitCode)
    {
        return new UnityTestExecutionResult(processExitCode, null, null, null);
    }

    /// <summary> Creates a failed execution result. </summary>
    /// <param name="failureKind"> The failure kind. </param>
    /// <param name="errorMessage"> The user-facing error message. </param>
    /// <param name="errorCode"> The machine-readable execution error code when one is available. </param>
    /// <returns> The failed execution result. </returns>
    public static UnityTestExecutionResult Failure (
        UnityTestExecutionFailureKind failureKind,
        string errorMessage,
        UcliErrorCode? errorCode = null,
        StartupFailureDetail? startupFailure = null)
    {
        return new UnityTestExecutionResult(
            null,
            failureKind,
            errorMessage,
            errorCode.HasValue && errorCode.Value.IsValid
                ? errorCode.Value
                : (UcliErrorCode?)null,
            startupFailure);
    }
}
