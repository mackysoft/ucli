using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Implements polling for GUI daemon session registration from an existing Unity Editor process. </summary>
internal sealed class DaemonGuiSessionRegistrationAwaiter : IDaemonGuiSessionRegistrationAwaiter
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly DaemonSessionProbe daemonSessionProbe;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    /// <summary> Initializes a new instance of the <see cref="DaemonGuiSessionRegistrationAwaiter" /> class. </summary>
    public DaemonGuiSessionRegistrationAwaiter (
        IDaemonSessionStore daemonSessionStore,
        DaemonSessionProbe daemonSessionProbe,
        IDaemonReachabilityClassifier reachabilityClassifier)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonSessionProbe = daemonSessionProbe ?? throw new ArgumentNullException(nameof(daemonSessionProbe));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        ExecutionDeadline deadline,
        DateTimeOffset expectedProcessStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expectedProcessId, 0);
        ArgumentNullException.ThrowIfNull(deadline);
        expectedProcessStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            expectedProcessStartedAtUtc,
            nameof(expectedProcessStartedAtUtc));

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
                        expectedProcessId,
                        expectedProcessStartedAtUtc,
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

            await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), deadline.Clock, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<DaemonGuiSessionRegistrationWaitResult?> TryProbeSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        int expectedProcessId,
        DateTimeOffset expectedProcessStartedAtUtc,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return DaemonGuiSessionRegistrationWaitResult.Failure(CreateTimeoutError(
                $"Timed out before probing GUI daemon session. ProcessId={session.ProcessId}."));
        }

        var attemptDeadline = remainingTimeout <= DaemonTimeouts.ProbeAttemptTimeoutCap
            ? deadline
            : deadline.CreateCappedDeadline(DaemonTimeouts.ProbeAttemptTimeoutCap);
        var probeResult = await daemonSessionProbe.ProbeAsync(
                unityProject,
                session,
                attemptDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (probeResult.IsSuccess)
        {
            var respondingSession = probeResult.Session;
            var pingResponse = probeResult.PingResponse;
            return MatchesExpectedGuiSession(
                   respondingSession,
                   unityProject,
                   expectedProcessId,
                   expectedProcessStartedAtUtc)
                   && pingResponse.State.EditorMode == DaemonEditorMode.Gui
                ? DaemonGuiSessionRegistrationWaitResult.Success(respondingSession, pingResponse)
                : null;
        }

        if (probeResult.SessionReadFailure is not null)
        {
            return probeResult.SessionReadFailure.FailureKind == DaemonSessionReadFailureKind.InvalidSession
                ? null
                : DaemonGuiSessionRegistrationWaitResult.Failure(probeResult.SessionReadFailure.Error!);
        }

        var probeFailure = probeResult.ProbeFailure!;
        return probeFailure is TimeoutException
            || reachabilityClassifier.IsNotRunning(probeFailure)
            || reachabilityClassifier.IsSessionTokenInvalid(probeFailure)
            || reachabilityClassifier.IsRecoverableResponseInterruption(probeFailure)
                ? null
                : DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.InternalError(
                    $"Failed to probe GUI daemon session. {probeFailure.Message}"));
    }

    private static bool TryGetMatchingGuiSession (
        DaemonSessionReadResult readResult,
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset expectedProcessStartedAtUtc,
        out DaemonSession? session)
    {
        session = null;
        if (!readResult.IsSuccess || !readResult.Exists)
        {
            return false;
        }

        var candidate = readResult.Session!;
        if (!MatchesExpectedGuiSession(
                candidate,
                unityProject,
                expectedProcessId,
                expectedProcessStartedAtUtc))
        {
            return false;
        }

        session = candidate;
        return true;
    }

    private static bool MatchesExpectedGuiSession (
        DaemonSession candidate,
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset expectedProcessStartedAtUtc)
    {
        if (candidate.ProcessId != expectedProcessId)
        {
            return false;
        }

        if (candidate.ProcessStartedAtUtc is not DateTimeOffset candidateProcessStartedAtUtc)
        {
            return false;
        }

        if (!DaemonProcessStartTimeMatcher.Matches(candidateProcessStartedAtUtc, expectedProcessStartedAtUtc))
        {
            return false;
        }

        if (candidate.ProjectFingerprint != unityProject.ProjectFingerprint)
        {
            return false;
        }

        if (candidate.EditorMode != DaemonEditorMode.Gui)
        {
            return false;
        }

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
