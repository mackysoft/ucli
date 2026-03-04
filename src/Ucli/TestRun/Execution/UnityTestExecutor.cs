using MackySoft.Ucli.Execution;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Implements Unity test execution through external process invocation and artifact verification. </summary>
internal sealed class UnityTestExecutor : IUnityTestExecutor
{
    private readonly IUnityCommandBuilder unityCommandBuilder;

    private readonly IProcessRunner processRunner;

    /// <summary> Initializes a new instance of the <see cref="UnityTestExecutor" /> class. </summary>
    /// <param name="unityCommandBuilder"> The Unity command builder dependency. </param>
    /// <param name="processRunner"> The process runner dependency. </param>
    public UnityTestExecutor (
        IUnityCommandBuilder unityCommandBuilder,
        IProcessRunner processRunner)
    {
        this.unityCommandBuilder = unityCommandBuilder ?? throw new ArgumentNullException(nameof(unityCommandBuilder));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    /// <summary> Executes one Unity test run and validates required artifacts. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="timeout"> The execution timeout for one run. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by the caller. </param>
    /// <returns> A task that resolves to the Unity test execution result. </returns>
    public async ValueTask<UnityTestExecutionResult> Execute (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(artifactPaths);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var arguments = unityCommandBuilder.BuildArguments(configuration, artifactPaths);
        var timeoutSeconds = ConvertTimeoutMillisecondsToProcessTimeoutSeconds(timeout);

        ProcessRunResult processRunResult;
        try
        {
            processRunResult = await processRunner.RunAsync(
                new ProcessRunRequest(
                    FileName: configuration.UnityEditorPath,
                    Arguments: arguments,
                    TimeoutSeconds: timeoutSeconds),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.Canceled,
                "Unity process execution was canceled.");
        }

        switch (processRunResult.Status)
        {
            case ProcessRunStatus.StartFailed:
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.StartFailed,
                    processRunResult.ErrorMessage ?? "Failed to start Unity process.");

            case ProcessRunStatus.TimedOut:
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.TimedOut,
                    processRunResult.ErrorMessage ?? $"Unity process timed out after {timeoutSeconds} seconds.");

            case ProcessRunStatus.Canceled:
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.Canceled,
                    processRunResult.ErrorMessage ?? "Unity process execution was canceled.");

            case ProcessRunStatus.Exited:
                if (!processRunResult.ExitCode.HasValue)
                {
                    return UnityTestExecutionResult.Failure(
                        UnityTestExecutionFailureKind.StartFailed,
                        "Unity process exit code was unavailable.");
                }

                if (processRunResult.ExitCode.Value != 0 && processRunResult.ExitCode.Value != 2)
                {
                    return UnityTestExecutionResult.Failure(
                        UnityTestExecutionFailureKind.AbnormalExit,
                        processRunResult.ErrorMessage ?? $"Unity process exited with code {processRunResult.ExitCode.Value}.");
                }

                break;

            default:
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.StartFailed,
                    "Unity process execution status is unknown.");
        }

        if (!File.Exists(artifactPaths.ResultsXmlPath))
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ArtifactMissing,
                $"Unity process completed but results.xml was not generated: {artifactPaths.ResultsXmlPath}");
        }

        if (!File.Exists(artifactPaths.EditorLogPath))
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ArtifactMissing,
                $"Unity process completed but editor.log was not generated: {artifactPaths.EditorLogPath}");
        }

        return UnityTestExecutionResult.Success(processRunResult.ExitCode!.Value);
    }

    /// <summary> Converts one millisecond timeout to process timeout seconds with ceil and minimum one second. </summary>
    /// <param name="timeout"> The timeout to convert. </param>
    /// <returns> The converted timeout in seconds. </returns>
    private static int ConvertTimeoutMillisecondsToProcessTimeoutSeconds (TimeSpan timeout)
    {
        var timeoutSeconds = (int)Math.Ceiling(timeout.TotalSeconds);
        if (timeoutSeconds < 1)
        {
            return 1;
        }

        return timeoutSeconds;
    }
}