using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Features.Testing.Run.Execution;

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
        ProcessRunResult processRunResult;
        try
        {
            processRunResult = await processRunner.RunAsync(
                new ProcessRunRequest(
                    FileName: configuration.UnityEditorPath,
                    Arguments: arguments,
                    Timeout: timeout),
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
                    UnityTestExecutionFailureKind.ProcessTimedOut,
                    processRunResult.ErrorMessage ?? $"Unity process timed out after {timeout.TotalMilliseconds:0} milliseconds.");

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

        if (!TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var artifactValidationError))
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ArtifactMissing,
                artifactValidationError!);
        }

        return UnityTestExecutionResult.Success(processRunResult.ExitCode!.Value);
    }
}
