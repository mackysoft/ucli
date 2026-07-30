namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Represents one observed Unity test-execution outcome. </summary>
internal abstract record UnityTestExecutionResult
{
    private const int SuccessfulProcessExitCode = 0;
    private const int TestFailureObservedProcessExitCode = 2;

    private UnityTestExecutionResult ()
    {
    }

    /// <summary> Classifies one observed Unity process exit code. </summary>
    /// <param name="processExitCode"> The observed Unity process exit code. </param>
    /// <returns> A completed observation for a supported code; otherwise an abnormal-exit failure. </returns>
    public static UnityTestExecutionResult FromProcessExitCode (int processExitCode)
    {
        return IsSupportedProcessExitCode(processExitCode)
            ? new ObservedProcessCompletion(processExitCode)
            : new AbnormalProcessExitFailure(processExitCode);
    }

    public static ExecutionFailure StartFailed (
        string errorMessage,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure)
    {
        return CreateFailure(
            UnityTestExecutionFailureKind.StartFailed,
            errorMessage,
            errorCode,
            startupFailure);
    }

    public static ExecutionFailure IpcTimedOut (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.IpcTimedOut, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure ProcessTimedOut (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.ProcessTimedOut, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure Canceled (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.Canceled, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure ArtifactMissing (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.ArtifactMissing, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure ProgressProtocolViolation (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.ProgressProtocolViolation, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure IpcTransportInterrupted (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.IpcTransportInterrupted, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure RequestFailed (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.RequestFailed, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure InvalidResponse (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.InvalidResponse, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure InternalError (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.InternalError, errorMessage, errorCode, startupFailure: null);

    public static ExecutionFailure InvalidArgument (string errorMessage, UcliCode errorCode) =>
        CreateFailure(UnityTestExecutionFailureKind.InvalidArgument, errorMessage, errorCode, startupFailure: null);

    /// <summary> Represents completion observed from the Unity process exit code. </summary>
    internal sealed record ObservedProcessCompletion : UnityTestExecutionResult
    {
        internal ObservedProcessCompletion (int processExitCode)
        {
            if (!IsSupportedProcessExitCode(processExitCode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(processExitCode),
                    processExitCode,
                    "A completed Unity test process must report exit code 0 or 2.");
            }

            ProcessExitCode = processExitCode;
        }

        /// <summary> Gets the observed supported Unity process exit code. </summary>
        public int ProcessExitCode { get; }
    }

    /// <summary> Represents an execution failure with complete diagnostic evidence. </summary>
    internal abstract record ExecutionFailure : UnityTestExecutionResult
    {
        protected ExecutionFailure (
            UnityTestExecutionFailureKind failureKind,
            string errorMessage,
            UcliCode errorCode,
            StartupFailureDetail? startupFailure)
        {
            if (!Enum.IsDefined(failureKind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(failureKind),
                    failureKind,
                    "Unity test execution failure kind must be a defined value.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
            ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
            FailureKind = failureKind;
            ErrorMessage = errorMessage;
            StartupFailure = startupFailure;
        }

        /// <summary> Gets the execution failure kind. </summary>
        public UnityTestExecutionFailureKind FailureKind { get; }

        /// <summary> Gets the user-facing failure message. </summary>
        public string ErrorMessage { get; }

        /// <summary> Gets the machine-readable failure code. </summary>
        public UcliCode ErrorCode { get; }

        /// <summary> Gets the classified startup failure when one was established. </summary>
        public StartupFailureDetail? StartupFailure { get; }
    }

    /// <summary> Represents a classified failure without abnormal process-exit evidence. </summary>
    private sealed record ClassifiedExecutionFailure : ExecutionFailure
    {
        internal ClassifiedExecutionFailure (
            UnityTestExecutionFailureKind failureKind,
            string errorMessage,
            UcliCode errorCode,
            StartupFailureDetail? startupFailure)
            : base(
                RequireNonAbnormalExit(failureKind),
                errorMessage,
                errorCode,
                startupFailure)
        {
        }

        private static UnityTestExecutionFailureKind RequireNonAbnormalExit (
            UnityTestExecutionFailureKind failureKind)
        {
            if (failureKind == UnityTestExecutionFailureKind.AbnormalExit)
            {
                throw new ArgumentException(
                    "Use UnsupportedProcessExit when an unsupported process exit code was observed.",
                    nameof(failureKind));
            }

            return failureKind;
        }
    }

    /// <summary> Represents failure established from an observed unsupported Unity process exit code. </summary>
    internal sealed record AbnormalProcessExitFailure : ExecutionFailure
    {
        internal AbnormalProcessExitFailure (int processExitCode)
            : base(
                UnityTestExecutionFailureKind.AbnormalExit,
                CreateErrorMessage(processExitCode),
                TestRunErrorCodes.UnityTestExecutionFailed,
                startupFailure: null)
        {
            if (IsSupportedProcessExitCode(processExitCode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(processExitCode),
                    processExitCode,
                    "An abnormal process exit must carry an observed unsupported exit code.");
            }

            ProcessExitCode = processExitCode;
        }

        /// <summary> Gets the observed unsupported Unity process exit code. </summary>
        public int ProcessExitCode { get; }

        private static string CreateErrorMessage (int processExitCode)
        {
            return $"Unity test run returned unsupported exit code: {processExitCode}.";
        }
    }

    private static bool IsSupportedProcessExitCode (int processExitCode)
    {
        return processExitCode is SuccessfulProcessExitCode or TestFailureObservedProcessExitCode;
    }

    private static ExecutionFailure CreateFailure (
        UnityTestExecutionFailureKind failureKind,
        string errorMessage,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure)
    {
        return new ClassifiedExecutionFailure(
            failureKind,
            errorMessage,
            errorCode,
            startupFailure);
    }
}
