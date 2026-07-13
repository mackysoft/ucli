namespace MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;

/// <summary> Represents normalized output returned from test-run core service. </summary>
internal sealed record TestRunServiceResult
{
    private TestRunServiceResult (
        TestRunResultKind? result,
        TestRunErrorKind? errorKind,
        ApplicationFailure? failure,
        string message,
        Guid? runId,
        string? artifactsDir,
        string? summaryJsonPath,
        StartupFailureDetail? startupFailure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(runId));
        }

        if (errorKind is null)
        {
            if (result is null)
            {
                throw new ArgumentException("Successful test-run result must contain a result value.", nameof(result));
            }

            if (failure is not null)
            {
                throw new ArgumentException("Successful test-run result must not contain a failure.", nameof(failure));
            }
        }
        else
        {
            if (result is not null)
            {
                throw new ArgumentException("Failed test-run result must not contain a pass/fail result.", nameof(result));
            }

            ArgumentNullException.ThrowIfNull(failure);
        }

        Result = result;
        ErrorKind = errorKind;
        Failure = failure;
        Message = message;
        RunId = runId;
        ArtifactsDir = artifactsDir;
        SummaryJsonPath = summaryJsonPath;
        StartupFailure = startupFailure;
    }

    /// <summary> Gets the pass/fail result when execution reaches test result evaluation. </summary>
    public TestRunResultKind? Result { get; }

    /// <summary> Gets the payload error kind when execution fails before test result evaluation. </summary>
    public TestRunErrorKind? ErrorKind { get; }

    /// <summary> Gets the classified failure when execution fails before test result evaluation. </summary>
    public ApplicationFailure? Failure { get; }

    /// <summary> Gets the application outcome. </summary>
    public ApplicationOutcome Outcome => Failure?.Outcome ?? Result switch
    {
        TestRunResultKind.Pass => ApplicationOutcome.Success,
        TestRunResultKind.Fail => ApplicationOutcome.TestFailure,
        _ => ApplicationOutcome.ToolError,
    };

    /// <summary> Gets the user-facing execution message. </summary>
    public string Message { get; }

    /// <summary> Gets the run identifier when artifacts session exists. </summary>
    public Guid? RunId { get; }

    /// <summary> Gets the run artifacts directory path when available. </summary>
    public string? ArtifactsDir { get; }

    /// <summary> Gets the summary JSON path when available. </summary>
    public string? SummaryJsonPath { get; }

    /// <summary> Gets the structured startup failure detail when Unity did not reach test execution. </summary>
    public StartupFailureDetail? StartupFailure { get; }

    /// <summary> Gets the machine-readable error code when execution fails. </summary>
    public UcliCode? ErrorCode => Failure?.Code;

    /// <summary> Gets the serialized result value used by command payload mapping. </summary>
    public string? ResultValue => Result switch
    {
        null => null,
        TestRunResultKind.Pass => "pass",
        TestRunResultKind.Fail => "fail",
        _ => null,
    };

    /// <summary> Gets the serialized error-kind value used by command payload mapping. </summary>
    public string? ErrorKindValue => ErrorKind switch
    {
        null => null,
        TestRunErrorKind.InvalidInput => "invalidInput",
        TestRunErrorKind.InfraError => "infraError",
        TestRunErrorKind.ToolError => "toolError",
        _ => null,
    };

    /// <summary> Creates a success result with pass state. </summary>
    /// <param name="message"> The user-facing message. </param>
    /// <param name="runId"> The run identifier. </param>
    /// <param name="artifactsDir"> The artifacts directory path. </param>
    /// <param name="summaryJsonPath"> The summary JSON path. </param>
    /// <returns> The pass result. </returns>
    public static TestRunServiceResult Pass (
        string message,
        Guid runId,
        string artifactsDir,
        string summaryJsonPath)
    {
        return new TestRunServiceResult(
            result: TestRunResultKind.Pass,
            errorKind: null,
            failure: null,
            message: message,
            runId: runId,
            artifactsDir: artifactsDir,
            summaryJsonPath: summaryJsonPath);
    }

    /// <summary> Creates a success result with fail state. </summary>
    /// <param name="message"> The user-facing message. </param>
    /// <param name="runId"> The run identifier. </param>
    /// <param name="artifactsDir"> The artifacts directory path. </param>
    /// <param name="summaryJsonPath"> The summary JSON path. </param>
    /// <returns> The fail result. </returns>
    public static TestRunServiceResult Fail (
        string message,
        Guid runId,
        string artifactsDir,
        string summaryJsonPath)
    {
        return new TestRunServiceResult(
            result: TestRunResultKind.Fail,
            errorKind: null,
            failure: null,
            message: message,
            runId: runId,
            artifactsDir: artifactsDir,
            summaryJsonPath: summaryJsonPath);
    }

    /// <summary> Creates an invalid-input error result. </summary>
    /// <param name="message"> The user-facing message. </param>
    /// <param name="errorCode"> The machine-readable error code. </param>
    /// <param name="runId"> The optional run identifier. </param>
    /// <param name="artifactsDir"> The optional artifacts directory path. </param>
    /// <param name="summaryJsonPath"> The optional summary JSON path. </param>
    /// <returns> The invalid-input error result. </returns>
    public static TestRunServiceResult InvalidInput (
        string message,
        UcliCode errorCode,
        Guid? runId = null,
        string? artifactsDir = null,
        string? summaryJsonPath = null,
        StartupFailureDetail? startupFailure = null)
    {
        return new TestRunServiceResult(
            result: null,
            errorKind: TestRunErrorKind.InvalidInput,
            failure: ApplicationFailure.InvalidInput(message, errorCode, startupFailure: startupFailure),
            message: message,
            runId: runId,
            artifactsDir: artifactsDir,
            summaryJsonPath: summaryJsonPath,
            startupFailure);
    }

    /// <summary> Creates an infrastructure error result. </summary>
    /// <param name="message"> The user-facing message. </param>
    /// <param name="errorCode"> The machine-readable error code. </param>
    /// <param name="runId"> The optional run identifier. </param>
    /// <param name="artifactsDir"> The optional artifacts directory path. </param>
    /// <param name="summaryJsonPath"> The optional summary JSON path. </param>
    /// <returns> The infrastructure error result. </returns>
    public static TestRunServiceResult InfraError (
        string message,
        UcliCode errorCode,
        Guid? runId = null,
        string? artifactsDir = null,
        string? summaryJsonPath = null,
        StartupFailureDetail? startupFailure = null)
    {
        return new TestRunServiceResult(
            result: null,
            errorKind: TestRunErrorKind.InfraError,
            failure: ApplicationFailure.ExternalProcessFailure(
                message,
                errorCode,
                outcome: ApplicationOutcome.InfrastructureError,
                startupFailure: startupFailure),
            message: message,
            runId: runId,
            artifactsDir: artifactsDir,
            summaryJsonPath: summaryJsonPath,
            startupFailure);
    }

    /// <summary> Creates a tool-error result. </summary>
    /// <param name="message"> The user-facing message. </param>
    /// <param name="errorCode"> The machine-readable error code. </param>
    /// <param name="runId"> The optional run identifier. </param>
    /// <param name="artifactsDir"> The optional artifacts directory path. </param>
    /// <param name="summaryJsonPath"> The optional summary JSON path. </param>
    /// <returns> The tool-error result. </returns>
    public static TestRunServiceResult ToolError (
        string message,
        UcliCode errorCode,
        Guid? runId = null,
        string? artifactsDir = null,
        string? summaryJsonPath = null,
        StartupFailureDetail? startupFailure = null)
    {
        return new TestRunServiceResult(
            result: null,
            errorKind: TestRunErrorKind.ToolError,
            failure: CreateToolFailure(message, errorCode, startupFailure),
            message: message,
            runId: runId,
            artifactsDir: artifactsDir,
            summaryJsonPath: summaryJsonPath,
            startupFailure);
    }

    private static ApplicationFailure CreateToolFailure (
        string message,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure)
    {
        if (errorCode == ExecutionErrorCodes.IpcTimeout || errorCode == TestRunErrorCodes.UnityTestExecutionTimeout)
        {
            return ApplicationFailure.Timeout(message, errorCode, startupFailure: startupFailure);
        }

        if (errorCode == ExecutionErrorCodes.Canceled)
        {
            return ApplicationFailure.Canceled(message, errorCode);
        }

        return ApplicationFailure.ExternalProcessFailure(message, errorCode, startupFailure: startupFailure);
    }
}
