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
        TimeProvider? timeProvider = null)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.timeProvider = timeProvider ?? TimeProvider.System;
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

            var readResult = await daemonSessionStore.ReadAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
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
            var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                    unityProject,
                    remainingTimeout,
                    session.SessionToken,
                    validateProjectFingerprint: false,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!DaemonStartLifecycleSnapshot.TryCreate(pingResponse, out var lifecycleSnapshot, out var lifecycleError))
            {
                return DaemonGuiSessionRegistrationWaitResult.Failure(lifecycleError!);
            }

            return string.Equals(pingResponse.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal)
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

        if (!string.Equals(candidate.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        if (!ContractLiteralCodec.Matches(candidate.EditorMode, DaemonEditorMode.Gui))
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
