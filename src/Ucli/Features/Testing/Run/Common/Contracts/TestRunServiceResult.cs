namespace MackySoft.Ucli.Features.Testing.Run.Common.Contracts;

/// <summary> Represents normalized output returned from test-run core service. </summary>
/// <param name="Result"> The pass/fail result when execution reaches test result evaluation; otherwise <see langword="null" />. </param>
/// <param name="ErrorKind"> The normalized error kind when execution fails; otherwise <see langword="null" />. </param>
/// <param name="ExitCode"> The numeric process exit code. </param>
/// <param name="Message"> The user-facing execution message. </param>
/// <param name="RunId"> The run identifier when artifacts session exists; otherwise <see langword="null" />. </param>
/// <param name="ArtifactsDir"> The run artifacts directory path when available; otherwise <see langword="null" />. </param>
/// <param name="SummaryJsonPath"> The summary JSON path when available; otherwise <see langword="null" />. </param>
/// <param name="ErrorCode"> The machine-readable error code when execution fails; otherwise <see langword="null" />. </param>
internal sealed record TestRunServiceResult (
    TestRunResultKind? Result,
    TestRunErrorKind? ErrorKind,
    int ExitCode,
    string Message,
    string? RunId,
    string? ArtifactsDir,
    string? SummaryJsonPath,
    string? ErrorCode)
{
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
        string runId,
        string artifactsDir,
        string summaryJsonPath)
    {
        return new TestRunServiceResult(
            Result: TestRunResultKind.Pass,
            ErrorKind: null,
            ExitCode: (int)TestRunExitCode.Pass,
            Message: message,
            RunId: runId,
            ArtifactsDir: artifactsDir,
            SummaryJsonPath: summaryJsonPath,
            ErrorCode: null);
    }

    /// <summary> Creates a success result with fail state. </summary>
    /// <param name="message"> The user-facing message. </param>
    /// <param name="runId"> The run identifier. </param>
    /// <param name="artifactsDir"> The artifacts directory path. </param>
    /// <param name="summaryJsonPath"> The summary JSON path. </param>
    /// <returns> The fail result. </returns>
    public static TestRunServiceResult Fail (
        string message,
        string runId,
        string artifactsDir,
        string summaryJsonPath)
    {
        return new TestRunServiceResult(
            Result: TestRunResultKind.Fail,
            ErrorKind: null,
            ExitCode: (int)TestRunExitCode.Fail,
            Message: message,
            RunId: runId,
            ArtifactsDir: artifactsDir,
            SummaryJsonPath: summaryJsonPath,
            ErrorCode: null);
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
        string errorCode,
        string? runId = null,
        string? artifactsDir = null,
        string? summaryJsonPath = null)
    {
        return new TestRunServiceResult(
            Result: null,
            ErrorKind: TestRunErrorKind.InvalidInput,
            ExitCode: (int)TestRunExitCode.InvalidInput,
            Message: message,
            RunId: runId,
            ArtifactsDir: artifactsDir,
            SummaryJsonPath: summaryJsonPath,
            ErrorCode: errorCode);
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
        string errorCode,
        string? runId = null,
        string? artifactsDir = null,
        string? summaryJsonPath = null)
    {
        return new TestRunServiceResult(
            Result: null,
            ErrorKind: TestRunErrorKind.InfraError,
            ExitCode: (int)TestRunExitCode.InfraError,
            Message: message,
            RunId: runId,
            ArtifactsDir: artifactsDir,
            SummaryJsonPath: summaryJsonPath,
            ErrorCode: errorCode);
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
        string errorCode,
        string? runId = null,
        string? artifactsDir = null,
        string? summaryJsonPath = null)
    {
        return new TestRunServiceResult(
            Result: null,
            ErrorKind: TestRunErrorKind.ToolError,
            ExitCode: (int)TestRunExitCode.ToolError,
            Message: message,
            RunId: runId,
            ArtifactsDir: artifactsDir,
            SummaryJsonPath: summaryJsonPath,
            ErrorCode: errorCode);
    }
}
