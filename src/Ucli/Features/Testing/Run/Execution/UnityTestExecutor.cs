using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Unity.Process;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Features.Testing.Run.Execution;

/// <summary> Implements Unity test execution through external process invocation and artifact verification. </summary>
internal sealed class UnityTestExecutor : IUnityTestExecutor
{
    private static readonly TimeSpan ProjectExecutionLockAcquireTimeout = TimeSpan.FromMilliseconds(100);

    private readonly IUnityCommandBuilder unityCommandBuilder;

    private readonly IProcessRunner processRunner;

    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IUnityProjectLockPreflightService unityProjectLockPreflightService;

    /// <summary> Initializes a new instance of the <see cref="UnityTestExecutor" /> class. </summary>
    /// <param name="unityCommandBuilder"> The Unity command builder dependency. </param>
    /// <param name="processRunner"> The process runner dependency. </param>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="unityProjectLockPreflightService"> The Unity project lock preflight service dependency. </param>
    public UnityTestExecutor (
        IUnityCommandBuilder unityCommandBuilder,
        IProcessRunner processRunner,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockPreflightService unityProjectLockPreflightService)
    {
        this.unityCommandBuilder = unityCommandBuilder ?? throw new ArgumentNullException(nameof(unityCommandBuilder));
        this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.unityProjectLockPreflightService = unityProjectLockPreflightService ?? throw new ArgumentNullException(nameof(unityProjectLockPreflightService));
    }

    /// <summary> Executes one Unity test run and validates required artifacts. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="timeout"> The execution timeout for one run. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by the caller. </param>
    /// <returns> A task that resolves to the Unity test execution result. </returns>
    public async ValueTask<UnityTestExecutionResult> ExecuteAsync (
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
            lifecycleLock = await lifecycleLockProvider.AcquireAsync(
                    new ProjectLifecycleLockRequest(configuration.UnityProject.UnityProjectRoot),
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
        var projectLockFailure = await TryCreateProjectLockFailureAsync(configuration.UnityProject, cancellationToken).ConfigureAwait(false);
        if (projectLockFailure != null)
        {
            return projectLockFailure;
        }

        var arguments = unityCommandBuilder.BuildArguments(configuration, artifactPaths);
        if (cancellationToken.IsCancellationRequested)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.Canceled,
                "Unity process execution was canceled.");
        }

        // NOTE: An external Unity editor can open the project after the first probe but before process start.
        projectLockFailure = await TryCreateProjectLockFailureAsync(configuration.UnityProject, cancellationToken).ConfigureAwait(false);
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
                    OutputDrainMode: ProcessOutputDrainMode.BestEffort,
                    TerminationPolicy: UnityProcessTerminationPolicy.GracefulThenKill),
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
                var timeoutMessage = await AppendPostTerminationLockFileDiagnosticAsync(
                        processRunResult.ErrorMessage ?? $"Unity process timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                        processRunResult.TerminationResult,
                        configuration.UnityProject)
                    .ConfigureAwait(false);
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.ProcessTimedOut,
                    timeoutMessage);

            case ProcessRunStatus.Canceled:
                var canceledMessage = await AppendPostTerminationLockFileDiagnosticAsync(
                        processRunResult.ErrorMessage ?? "Unity process execution was canceled.",
                        processRunResult.TerminationResult,
                        configuration.UnityProject)
                    .ConfigureAwait(false);
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.Canceled,
                    canceledMessage);

            case ProcessRunStatus.Exited:
                if (!processRunResult.ExitCode.HasValue)
                {
                    return UnityTestExecutionResult.Failure(
                        UnityTestExecutionFailureKind.StartFailed,
                        "Unity process exit code was unavailable.");
                }

                if (processRunResult.ExitCode.Value != 0 && processRunResult.ExitCode.Value != 2)
                {
                    var abnormalExitMessage = await AppendPostUnityProcessExitLockFileDiagnosticAsync(
                            processRunResult.ErrorMessage ?? $"Unity process exited with code {processRunResult.ExitCode.Value}.",
                            configuration.UnityProject)
                        .ConfigureAwait(false);
                    return UnityTestExecutionResult.Failure(
                        UnityTestExecutionFailureKind.AbnormalExit,
                        abnormalExitMessage);
                }

                break;

            default:
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.StartFailed,
                    "Unity process execution status is unknown.");
        }

        if (!TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var artifactValidationError))
        {
            var artifactFailureMessage = await AppendPostUnityProcessExitLockFileDiagnosticAsync(
                    artifactValidationError!,
                    configuration.UnityProject)
                .ConfigureAwait(false);
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ArtifactMissing,
                artifactFailureMessage);
        }

        return UnityTestExecutionResult.Success(processRunResult.ExitCode!.Value);
    }

    /// <summary> Creates a startup failure when Unity project lock preflight blocks process launch. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A classified project-lock failure when Unity owns the lock file; otherwise <see langword="null" />. </returns>
    private async ValueTask<UnityTestExecutionResult?> TryCreateProjectLockFailureAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var preflightResult = await unityProjectLockPreflightService.PrepareForUnityProcessStartAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);
        var error = UnityProjectLockPreflightErrorFactory.CreateLaunchBlockingError(unityProject, preflightResult);
        if (error == null)
        {
            return null;
        }

        return UnityTestExecutionResult.Failure(
            ResolveProjectLockFailureKind(preflightResult),
            error.Message,
            error.Code);
    }

    /// <summary> Appends a post-exit Unity lock-file cleanup diagnostic after uCLI has terminated a Unity process. </summary>
    /// <param name="message"> The primary failure message. Must not be null. </param>
    /// <param name="terminationResult"> The observed termination result. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <returns> The original message, or the message with a post-exit cleanup diagnostic appended. </returns>
    private ValueTask<string> AppendPostTerminationLockFileDiagnosticAsync (
        string message,
        ProcessTerminationResult terminationResult,
        ResolvedUnityProjectContext unityProject)
    {
        if (terminationResult == ProcessTerminationResult.None)
        {
            return ValueTask.FromResult(message);
        }

        // NOTE: Post-exit UnityLockfile cleanup is diagnostic only; timeout or cancellation remains the primary failure.
        return AppendPostUnityProcessExitLockFileDiagnosticAsync(message, unityProject);
    }

    private async ValueTask<string> AppendPostUnityProcessExitLockFileDiagnosticAsync (
        string message,
        ResolvedUnityProjectContext unityProject)
    {
        var preflightResult = await unityProjectLockPreflightService.CleanupStaleLockAfterUnityProcessExitAsync(
                unityProject,
                CancellationToken.None)
            .ConfigureAwait(false);
        return UnityProjectLockPreflightErrorFactory.AppendPostExitDiagnostic(message, preflightResult);
    }

    private static UnityTestExecutionFailureKind ResolveProjectLockFailureKind (UnityProjectLockPreflightResult preflightResult)
    {
        return preflightResult.Status == UnityProjectLockPreflightStatus.ActiveLock
            ? UnityTestExecutionFailureKind.ProjectAlreadyOpen
            : UnityTestExecutionFailureKind.StartFailed;
    }

}
