namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Represents one Unity test-execution result. </summary>
internal sealed record UnityTestExecutionResult
{
    private UnityTestExecutionResult (
        int? processExitCode,
        UnityTestExecutionFailureKind? failureKind,
        string? errorMessage,
        UcliCode? errorCode,
        StartupFailureDetail? startupFailure)
    {
        if (processExitCode.HasValue)
        {
            if (failureKind is not null || errorMessage is not null || errorCode is not null || startupFailure is not null)
            {
                throw new ArgumentException("Successful test execution must not contain failure details.", nameof(failureKind));
            }
        }
        else
        {
            ArgumentNullException.ThrowIfNull(failureKind);
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        }

        ProcessExitCode = processExitCode;
        FailureKind = failureKind;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        StartupFailure = startupFailure;
    }

    public int? ProcessExitCode { get; }

    public UnityTestExecutionFailureKind? FailureKind { get; }

    public string? ErrorMessage { get; }

    public UcliCode? ErrorCode { get; }

    public StartupFailureDetail? StartupFailure { get; }

    /// <summary> Gets a value indicating whether execution succeeded. </summary>
    public bool IsSuccess => ProcessExitCode.HasValue;

    /// <summary> Creates a successful execution result. </summary>
    /// <param name="processExitCode"> The Unity process exit code. </param>
    /// <returns> The successful execution result. </returns>
    public static UnityTestExecutionResult Success (int processExitCode)
    {
        return new UnityTestExecutionResult(processExitCode, null, null, null, null);
    }

    /// <summary> Creates a failed execution result. </summary>
    /// <param name="failureKind"> The failure kind. </param>
    /// <param name="errorMessage"> The user-facing error message. </param>
    /// <param name="errorCode"> The machine-readable execution error code when one is available. </param>
    /// <returns> The failed execution result. </returns>
    public static UnityTestExecutionResult Failure (
        UnityTestExecutionFailureKind failureKind,
        string errorMessage,
        UcliCode? errorCode = null,
        StartupFailureDetail? startupFailure = null)
    {
        return new UnityTestExecutionResult(
            null,
            failureKind,
            errorMessage,
            errorCode,
            startupFailure);
    }
}
