using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Implements polling for GUI daemon session registration from an existing Unity Editor process. </summary>
internal sealed class DaemonGuiSessionRegistrationAwaiter : IDaemonGuiSessionRegistrationAwaiter
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonGuiSessionRegistrationAwaiter" /> class. </summary>
    public DaemonGuiSessionRegistrationAwaiter (
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        TimeProvider timeProvider)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        TimeSpan timeout,
        DateTimeOffset? expectedProcessStartedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expectedProcessId, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return DaemonGuiSessionRegistrationWaitResult.Failure(CreateTimeoutError(
                    $"Timed out while waiting for GUI daemon session registration. ProcessId={expectedProcessId}."));
            }

            var readOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                    deadline,
                    cancellationToken,
                    $"Timed out before reading GUI daemon session registration. ProcessId={expectedProcessId}.",
                    $"Timed out while reading GUI daemon session registration. ProcessId={expectedProcessId}.",
                    token => daemonSessionStore.ReadAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        token))
                .ConfigureAwait(false);
            if (!readOperation.IsSuccess)
            {
                return DaemonGuiSessionRegistrationWaitResult.Failure(
                    CreateTimeoutError(readOperation.Error!.Message));
            }

            var readResult = readOperation.Value!;
            if (!readResult.IsSuccess && readResult.FailureKind != DaemonSessionReadFailureKind.InvalidSession)
            {
                return DaemonGuiSessionRegistrationWaitResult.Failure(readResult.Error!);
            }

            if (TryGetMatchingGuiSession(
                readResult,
                unityProject,
                expectedProcessId,
                expectedProcessStartedAtUtc,
                out var session))
            {
                var probeResult = await TryProbeSessionAsync(
                        unityProject,
                        session!,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (probeResult is not null)
                {
                    return probeResult;
                }
            }

            if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
            {
                return DaemonGuiSessionRegistrationWaitResult.Failure(CreateTimeoutError(
                    $"Timed out while waiting for GUI daemon session registration. ProcessId={expectedProcessId}."));
            }

            await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<DaemonGuiSessionRegistrationWaitResult?> TryProbeSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return DaemonGuiSessionRegistrationWaitResult.Failure(CreateTimeoutError(
                $"Timed out before probing GUI daemon session. ProcessId={session.ProcessId}."));
        }

        try
        {
            var attemptTimeout = remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
                ? remainingTimeout
                : DaemonTimeouts.ProbeAttemptTimeoutCap;
            var pingResponse = await daemonPingInfoClient.PingSessionAndReadAsync(
                    unityProject,
                    session,
                    attemptTimeout,
                    validateProjectFingerprint: false,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!DaemonStartLifecycleSnapshot.TryCreate(pingResponse, out var lifecycleSnapshot, out var lifecycleError))
            {
                return DaemonGuiSessionRegistrationWaitResult.Failure(lifecycleError!);
            }

            return pingResponse.ProjectFingerprint == unityProject.ProjectFingerprint
                   && ContractLiteralCodec.Matches(pingResponse.EditorMode, DaemonEditorMode.Gui)
                ? DaemonGuiSessionRegistrationWaitResult.Success(session, lifecycleSnapshot)
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            return null;
        }
        catch (Exception exception)
        {
            return DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.InternalError(
                $"Failed to probe GUI daemon session. {exception.Message}"));
        }
    }

    private static bool TryGetMatchingGuiSession (
        DaemonSessionReadResult readResult,
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        out DaemonSession? session)
    {
        session = null;
        if (!readResult.IsSuccess || !readResult.Exists)
        {
            return false;
        }

        var candidate = readResult.Session!;
        if (candidate.ProcessId != expectedProcessId)
        {
            return false;
        }

        if (expectedProcessStartedAtUtc is not null)
        {
            if (candidate.ProcessStartedAtUtc is not DateTimeOffset candidateProcessStartedAtUtc)
            {
                return false;
            }

            if (!DaemonProcessStartTimeMatcher.Matches(candidateProcessStartedAtUtc, expectedProcessStartedAtUtc.Value))
            {
                return false;
            }
        }

        if (candidate.ProjectFingerprint != unityProject.ProjectFingerprint)
        {
            return false;
        }

        if (candidate.EditorMode != DaemonEditorMode.Gui)
        {
            return false;
        }

        session = candidate;
        return true;
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }

    private static ExecutionError CreateTimeoutError (string message)
    {
        return ExecutionError.Timeout(message, ExecutionErrorCodes.IpcTimeout);
    }
}
