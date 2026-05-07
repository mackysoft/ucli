using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Features.Testing.Run.Execution;

/// <summary> Implements Unity test execution through external process invocation and artifact verification. </summary>
internal sealed class UnityTestExecutor : IUnityTestExecutor
{
    private static readonly TimeSpan ProjectExecutionLockAcquireTimeout = TimeSpan.FromMilliseconds(100);

    private readonly IUnityCommandBuilder unityCommandBuilder;

    private readonly IProcessRunner processRunner;

    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IUnityProjectLockFileProbe unityProjectLockFileProbe;

    /// <summary> Initializes a new instance of the <see cref="UnityTestExecutor" /> class. </summary>
    /// <param name="unityCommandBuilder"> The Unity command builder dependency. </param>
    /// <param name="processRunner"> The process runner dependency. </param>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="unityProjectLockFileProbe"> The Unity project lock-file probe dependency. </param>
    public UnityTestExecutor (
        IUnityCommandBuilder unityCommandBuilder,
        IProcessRunner processRunner,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockFileProbe unityProjectLockFileProbe)
    {
        this.unityCommandBuilder = unityCommandBuilder ?? throw new ArgumentNullException(nameof(unityCommandBuilder));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.unityProjectLockFileProbe = unityProjectLockFileProbe ?? throw new ArgumentNullException(nameof(unityProjectLockFileProbe));
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

        // NOTE: The uCLI lifecycle lock closes races between uCLI invocations; UnityLockfile catches editors opened outside uCLI.
        IAsyncDisposable lifecycleLock;
        try
        {
            lifecycleLock = await lifecycleLockProvider.Acquire(
                    configuration.UnityProject,
                    ProjectExecutionLockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ProjectAlreadyOpen,
                UnityProjectLockFailureMessage.CreateAlreadyOpen(configuration.UnityProject.UnityProjectRoot),
                UnityProcessErrorCodes.UnityProjectAlreadyOpen);
        }
        catch (Exception exception)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.StartFailed,
                $"Failed to acquire project lifecycle lock. {exception.Message}",
                UcliCoreErrorCodes.InternalError);
        }

        await using var acquiredLifecycleLock = lifecycleLock;
        var projectLockFailure = TryCreateProjectLockFailure(configuration.UnityProject.UnityProjectRoot);
        if (projectLockFailure != null)
        {
            return projectLockFailure;
        }

        var arguments = unityCommandBuilder.BuildArguments(configuration, artifactPaths);
        // NOTE: An external Unity editor can open the project after the first probe but before process start.
        projectLockFailure = TryCreateProjectLockFailure(configuration.UnityProject.UnityProjectRoot);
        if (projectLockFailure != null)
        {
            return projectLockFailure;
        }

        ProcessRunResult processRunResult;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            processRunResult = await processRunner.RunAsync(
                new ProcessRunRequest(
                    FileName: configuration.UnityEditorPath,
                    Arguments: arguments,
                    Timeout: timeout,
                    OutputDrainMode: ProcessOutputDrainMode.BestEffort),
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
                    var postExecutionProjectLockFailure = TryCreateProjectLockFailure(configuration.UnityProject.UnityProjectRoot);
                    if (postExecutionProjectLockFailure != null)
                    {
                        return postExecutionProjectLockFailure;
                    }

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
            var postExecutionProjectLockFailure = TryCreateProjectLockFailure(configuration.UnityProject.UnityProjectRoot);
            if (postExecutionProjectLockFailure != null)
            {
                return postExecutionProjectLockFailure;
            }

            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ArtifactMissing,
                artifactValidationError!);
        }

        return UnityTestExecutionResult.Success(processRunResult.ExitCode!.Value);
    }

    private UnityTestExecutionResult? TryCreateProjectLockFailure (string unityProjectRoot)
    {
        var lockFileProbeResult = unityProjectLockFileProbe.Probe(unityProjectRoot);
        if (!lockFileProbeResult.IsSuccess)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.StartFailed,
                lockFileProbeResult.ErrorMessage!,
                UcliCoreErrorCodes.InternalError);
        }

        if (!lockFileProbeResult.IsLocked)
        {
            return null;
        }

        return UnityTestExecutionResult.Failure(
            UnityTestExecutionFailureKind.ProjectAlreadyOpen,
            UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProjectRoot, lockFileProbeResult.LockFilePath),
            UnityProcessErrorCodes.UnityProjectAlreadyOpen);
    }

}
