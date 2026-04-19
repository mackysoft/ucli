using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Runtime;
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
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Features.Testing.Run.Execution;

/// <summary> Implements daemon-backed Unity test-run execution over IPC transport. </summary>
internal sealed class IpcDaemonTestRunClient : IDaemonTestRunClient
{
    private readonly IUnityIpcTransportClient transportClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonTestRunClient" /> class. </summary>
    /// <param name="transportClient"> The shared Unity IPC transport client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    public IpcDaemonTestRunClient (
        IUnityIpcTransportClient transportClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider,
        TimeProvider? timeProvider = null)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Executes one Unity test run through daemon IPC and validates generated artifacts. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <param name="timeout"> The remaining execution timeout budget for the daemon path. </param>
    /// <param name="failFast"> Whether daemon execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the Unity test execution result. </returns>
    public async ValueTask<UnityTestExecutionResult> Execute (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths,
        TimeSpan timeout,
        bool failFast,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(artifactPaths);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        try
        {
            var sessionTokenResult = await ResolveSessionToken(
                    configuration,
                    deadline,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sessionTokenResult.FailureResult is not null)
            {
                return sessionTokenResult.FailureResult;
            }

            if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
            {
                return CreateTimeoutFailure(timeout);
            }

            var request = IpcDaemonTestRunRequestCodec.CreateRequest(
                configuration,
                artifactPaths,
                sessionTokenResult.SessionToken!,
                failFast);
            var response = await transportClient.SendAsync(
                    configuration.UnityProject.RepositoryRoot,
                    configuration.UnityProject.ProjectFingerprint,
                    request,
                    requestTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IpcDaemonTestRunResponseCodec.TryDecode(response, out var exitCode, out var errorCode, out var errorMessage))
            {
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.AbnormalExit,
                    errorMessage!,
                    errorCode);
            }

            if (!TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var artifactValidationError))
            {
                return UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.ArtifactMissing,
                    artifactValidationError!);
            }

            return UnityTestExecutionResult.Success(exitCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.Canceled,
                "Unity daemon test run execution was canceled.");
        }
        catch (TimeoutException)
        {
            return CreateTimeoutFailure(timeout);
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.StartFailed,
                $"Unity daemon is not running. {exception.Message}",
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
        }
        catch (Exception exception)
        {
            return UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.AbnormalExit,
                $"Unexpected error during Unity daemon test run execution: {exception.Message}");
        }
    }

    /// <summary> Resolves the daemon session token from local daemon session storage. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="deadline"> The shared execution deadline. </param>
    /// <param name="timeout"> The original daemon execution timeout budget. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> One tuple containing the resolved session token or a normalized failure result. </returns>
    private async ValueTask<(string? SessionToken, UnityTestExecutionResult? FailureResult)> ResolveSessionToken (
        ResolvedTestRunConfiguration configuration,
        ExecutionDeadline deadline,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var sessionTokenTimeout))
        {
            return (null, CreateTimeoutFailure(timeout));
        }

        using var sessionTokenCancellationScope = TimeProviderCancellationScope.CreateLinked(
            cancellationToken,
            sessionTokenTimeout,
            timeProvider);

        DaemonSessionTokenResolutionResult sessionTokenResult;
        try
        {
            sessionTokenResult = await daemonSessionTokenProvider.Resolve(
                    configuration.UnityProject,
                    sessionTokenCancellationScope.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && sessionTokenCancellationScope.HasTimedOut)
        {
            return (null, CreateTimeoutFailure(timeout));
        }

        if (!sessionTokenResult.IsSuccess)
        {
            if (sessionTokenResult.IsSessionNotAvailable)
            {
                return (
                    null,
                    UnityTestExecutionResult.Failure(
                        UnityTestExecutionFailureKind.StartFailed,
                        "Unity daemon is not running. Daemon session token is not available.",
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning));
            }

            var error = sessionTokenResult.Error!;
            if (error.Kind == ExecutionErrorKind.Timeout)
            {
                return (null, CreateTimeoutFailure(timeout));
            }

            return (
                null,
                UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.ClientSetupFailed,
                    $"Daemon session token could not be resolved. {error.Message}",
                    ExecutionErrorKindCodeMapper.ToCode(error.Kind)));
        }

        return (sessionTokenResult.Token!, null);
    }

    private static UnityTestExecutionResult CreateTimeoutFailure (TimeSpan timeout)
    {
        return UnityTestExecutionResult.Failure(
            UnityTestExecutionFailureKind.IpcTimedOut,
            $"Unity daemon test run request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
    }
}