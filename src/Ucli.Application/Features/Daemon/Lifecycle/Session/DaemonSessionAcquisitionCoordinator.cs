namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Identifies the terminal outcome of acquiring a daemon session snapshot. </summary>
internal enum DaemonSessionAcquisitionKind
{
    Success,
    RequestDeadlineExpired,
    PublicationWindowExpired,
    EndpointAvailabilityWindowExpired,
    HostIdentityMismatch,
    SessionNotAvailable,
    SessionReadFailure,
}

/// <summary> Carries one validated daemon session snapshot or its explicit terminal reason. </summary>
internal sealed class DaemonSessionAcquisitionResult
{
    /// <summary> Gets the message used when daemon session metadata is not present. </summary>
    public const string SessionNotAvailableMessage = "Daemon session is not available.";

    private DaemonSessionAcquisitionResult (
        DaemonSessionAcquisitionKind kind,
        DaemonSession? session,
        DaemonSessionReadResult? readFailure)
    {
        if (kind == DaemonSessionAcquisitionKind.Success)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            if (readFailure is not null)
            {
                throw new ArgumentException("A successful session acquisition must not contain a read failure.", nameof(readFailure));
            }
        }
        else if (kind == DaemonSessionAcquisitionKind.SessionReadFailure)
        {
            if (session is not null)
            {
                throw new ArgumentException("A failed session acquisition must not contain a session.", nameof(session));
            }

            ReadFailure = readFailure ?? throw new ArgumentNullException(nameof(readFailure));
            if (readFailure.IsSuccess)
            {
                throw new ArgumentException("A session acquisition read failure must contain a failed store read.", nameof(readFailure));
            }
        }
        else if (kind is DaemonSessionAcquisitionKind.RequestDeadlineExpired
            or DaemonSessionAcquisitionKind.PublicationWindowExpired
            or DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired
            or DaemonSessionAcquisitionKind.HostIdentityMismatch
            or DaemonSessionAcquisitionKind.SessionNotAvailable)
        {
            if (session is not null || readFailure is not null)
            {
                throw new ArgumentException("A terminal session acquisition must not contain session data.");
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Daemon session acquisition kind is unsupported.");
        }

        Kind = kind;
    }

    /// <summary> Gets the explicit acquisition outcome. </summary>
    public DaemonSessionAcquisitionKind Kind { get; }

    /// <summary> Gets the validated session snapshot on success. </summary>
    public DaemonSession? Session { get; }

    /// <summary> Gets the original failed store read, including failure kind and artifact identity. </summary>
    public DaemonSessionReadResult? ReadFailure { get; }

    /// <summary> Creates a successful acquisition. </summary>
    public static DaemonSessionAcquisitionResult Success (DaemonSession session)
    {
        return new DaemonSessionAcquisitionResult(
            DaemonSessionAcquisitionKind.Success,
            session,
            readFailure: null);
    }

    /// <summary> Creates a terminal acquisition without a store or identity error. </summary>
    public static DaemonSessionAcquisitionResult Terminal (DaemonSessionAcquisitionKind kind)
    {
        return new DaemonSessionAcquisitionResult(kind, session: null, readFailure: null);
    }

    /// <summary> Creates an acquisition that preserves one failed session store read. </summary>
    public static DaemonSessionAcquisitionResult ReadFailed (DaemonSessionReadResult readFailure)
    {
        return new DaemonSessionAcquisitionResult(
            DaemonSessionAcquisitionKind.SessionReadFailure,
            session: null,
            readFailure);
    }
}

/// <summary> Creates request-local scopes that own daemon session publication and endpoint availability state. </summary>
internal sealed class DaemonSessionAcquisitionCoordinator
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly DaemonSessionRecoveryWaiter recoveryWaiter;

    /// <summary> Initializes the session store and recovery evidence dependencies. </summary>
    public DaemonSessionAcquisitionCoordinator (
        IDaemonSessionStore daemonSessionStore,
        DaemonSessionRecoveryWaiter recoveryWaiter)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.recoveryWaiter = recoveryWaiter ?? throw new ArgumentNullException(nameof(recoveryWaiter));
    }

    /// <summary> Creates one stateful acquisition scope for a logical IPC request. </summary>
    public DaemonSessionAcquisitionScope CreateScope (ExecutionDeadline requestDeadline)
    {
        return new DaemonSessionAcquisitionScope(
            daemonSessionStore,
            recoveryWaiter,
            requestDeadline);
    }
}

/// <summary> Owns rejected generations and bounded retry windows for one logical IPC request. </summary>
internal sealed class DaemonSessionAcquisitionScope
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly DaemonSessionRecoveryWaiter recoveryWaiter;

    private readonly ExecutionDeadline requestDeadline;

    private HashSet<Guid>? rejectedGenerationIds;

    private Guid? endpointAvailabilityGenerationId;

    private ExecutionDeadline? endpointAvailabilityDeadline;

    private DaemonSession? durableReplayHostSession;

    internal DaemonSessionAcquisitionScope (
        IDaemonSessionStore daemonSessionStore,
        DaemonSessionRecoveryWaiter recoveryWaiter,
        ExecutionDeadline requestDeadline)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.recoveryWaiter = recoveryWaiter ?? throw new ArgumentNullException(nameof(recoveryWaiter));
        this.requestDeadline = requestDeadline ?? throw new ArgumentNullException(nameof(requestDeadline));
    }

    /// <summary> Resolves the currently published session while preserving the request deadline. </summary>
    public async ValueTask<DaemonSessionAcquisitionResult> ResolveCurrentAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        ResetEndpointAvailabilityWindow();
        return await ResolveCurrentCoreAsync(unityProject, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Reacquires any non-rejected host session published for the same project after a stateless response was interrupted. </summary>
    public async ValueTask<DaemonSessionAcquisitionResult> ResolveAfterStatelessResponseInterruptionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession interruptedSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(interruptedSession);
        cancellationToken.ThrowIfCancellationRequested();
        ResetEndpointAvailabilityWindow();
        endpointAvailabilityGenerationId = interruptedSession.SessionGenerationId;
        return await ResolveWithinEndpointAvailabilityAsync(
                unityProject,
                interruptedSession,
                interruptedSession,
                SessionSuccessorPolicy.AnyHostSuccessor,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Reacquires only a successor from the Unity host whose first durable mutation response was interrupted. </summary>
    public async ValueTask<DaemonSessionAcquisitionResult> ResolveAfterDurableResponseInterruptionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession interruptedSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(interruptedSession);
        cancellationToken.ThrowIfCancellationRequested();

        var requiredHostSession = durableReplayHostSession;
        if (requiredHostSession is null)
        {
            requiredHostSession = interruptedSession;
            durableReplayHostSession = requiredHostSession;
            ResetEndpointAvailabilityWindow();
        }
        else if (!MatchesHostIdentity(requiredHostSession, interruptedSession))
        {
            return DaemonSessionAcquisitionResult.Terminal(
                DaemonSessionAcquisitionKind.HostIdentityMismatch);
        }

        endpointAvailabilityGenerationId = interruptedSession.SessionGenerationId;
        return await ResolveWithinEndpointAvailabilityAsync(
                unityProject,
                interruptedSession,
                requiredHostSession,
                SessionSuccessorPolicy.SameHostSuccessor,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Resolves a session outside every generation rejected during this logical request. </summary>
    public async ValueTask<DaemonSessionAcquisitionResult> ResolveReplacementAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession rejectedSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(rejectedSession);
        cancellationToken.ThrowIfCancellationRequested();
        RecordRejectedGeneration(rejectedSession);
        ResetEndpointAvailabilityWindow();
        return await ResolveReplacementCoreAsync(
                unityProject,
                rejectedSession,
                rejectedSession,
                SessionSuccessorPolicy.AnyHostSuccessor,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Resolves a non-rejected generation from the Unity host whose first durable mutation response was interrupted. </summary>
    public async ValueTask<DaemonSessionAcquisitionResult> ResolveDurableReplacementAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession rejectedSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(rejectedSession);
        cancellationToken.ThrowIfCancellationRequested();
        var requiredHostSession = durableReplayHostSession
            ?? throw new InvalidOperationException(
                "A durable session replacement requires a prior durable response interruption.");
        RecordRejectedGeneration(rejectedSession);
        return await ResolveReplacementCoreAsync(
                unityProject,
                rejectedSession,
                requiredHostSession,
                SessionSuccessorPolicy.SameHostSuccessor,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonSessionAcquisitionResult> ResolveReplacementCoreAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession rejectedSession,
        DaemonSession hostIdentitySession,
        SessionSuccessorPolicy successorPolicy,
        CancellationToken cancellationToken)
    {
        ExecutionDeadline? publicationDeadline = null;

        while (true)
        {
            if (requestDeadline.IsExpired)
            {
                return RequestDeadlineExpired();
            }

            publicationDeadline ??= requestDeadline.CreateCappedDeadline(
                DaemonTimeouts.SessionPublicationRetryTimeout);
            var readResult = await ReadOnceAsync(
                    unityProject,
                    publicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (readResult is null)
            {
                if (requestDeadline.IsExpired)
                {
                    return RequestDeadlineExpired();
                }

                if (await recoveryWaiter.DelayIfRecoveringAsync(
                        unityProject,
                        rejectedSession,
                        requestDeadline,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    publicationDeadline = null;
                    continue;
                }

                return GetPublicationExpiredResult();
            }

            if (TryGetAcceptedSession(readResult, out var session))
            {
                if (successorPolicy == SessionSuccessorPolicy.SameHostSuccessor
                    && !MatchesHostIdentity(hostIdentitySession, session!))
                {
                    return DaemonSessionAcquisitionResult.Terminal(
                        DaemonSessionAcquisitionKind.HostIdentityMismatch);
                }

                return DaemonSessionAcquisitionResult.Success(session!);
            }

            if (!readResult.IsSuccess)
            {
                return DaemonSessionAcquisitionResult.ReadFailed(readResult);
            }

            if (await recoveryWaiter.DelayIfRecoveringAsync(
                    unityProject,
                    rejectedSession,
                    requestDeadline,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                publicationDeadline = null;
                continue;
            }

            if (requestDeadline.IsExpired)
            {
                return RequestDeadlineExpired();
            }

            if (!await DelayForPublicationAsync(
                    requestDeadline,
                    publicationDeadline!,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return GetPublicationExpiredResult();
            }
        }
    }

    /// <summary> Resolves the same or a newer non-rejected generation after a pre-write endpoint failure. </summary>
    public async ValueTask<DaemonSessionAcquisitionResult> ResolveAfterPreWriteFailureAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession failedSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(failedSession);
        cancellationToken.ThrowIfCancellationRequested();

        if (endpointAvailabilityGenerationId != failedSession.SessionGenerationId)
        {
            endpointAvailabilityGenerationId = failedSession.SessionGenerationId;
            endpointAvailabilityDeadline = null;
        }

        return await ResolveWithinEndpointAvailabilityAsync(
                unityProject,
                failedSession,
                failedSession,
                SessionSuccessorPolicy.AnyHostSuccessor,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Reacquires only the durable replay host after a connection failure before replay request transmission. </summary>
    public async ValueTask<DaemonSessionAcquisitionResult> ResolveAfterDurablePreWriteFailureAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession failedSession,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(failedSession);
        cancellationToken.ThrowIfCancellationRequested();
        var requiredHostSession = durableReplayHostSession
            ?? throw new InvalidOperationException(
                "A durable replay pre-write failure requires a prior durable response interruption.");
        if (!MatchesHostIdentity(requiredHostSession, failedSession))
        {
            return DaemonSessionAcquisitionResult.Terminal(
                DaemonSessionAcquisitionKind.HostIdentityMismatch);
        }

        endpointAvailabilityGenerationId = failedSession.SessionGenerationId;
        return await ResolveWithinEndpointAvailabilityAsync(
                unityProject,
                failedSession,
                requiredHostSession,
                SessionSuccessorPolicy.SameHostSuccessor,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonSessionAcquisitionResult> ResolveWithinEndpointAvailabilityAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession failedSession,
        DaemonSession hostIdentitySession,
        SessionSuccessorPolicy successorPolicy,
        CancellationToken cancellationToken)
    {
        var hasWaitedSinceDelivery = false;
        while (true)
        {
            if (requestDeadline.IsExpired)
            {
                return RequestDeadlineExpired();
            }

            endpointAvailabilityDeadline ??= requestDeadline.CreateCappedDeadline(
                DaemonTimeouts.ProbeAttemptTimeoutCap);
            var readResult = await ReadOnceAsync(
                    unityProject,
                    endpointAvailabilityDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (readResult is null)
            {
                if (requestDeadline.IsExpired)
                {
                    return RequestDeadlineExpired();
                }

                if (await recoveryWaiter.DelayIfRecoveringAsync(
                        unityProject,
                        failedSession,
                        requestDeadline,
                        cancellationToken)
                    .ConfigureAwait(false))
                {
                    endpointAvailabilityDeadline = null;
                    hasWaitedSinceDelivery = true;
                    continue;
                }

                return GetEndpointAvailabilityExpiredResult();
            }

            if (TryGetAcceptedSession(readResult, out var session))
            {
                if (successorPolicy == SessionSuccessorPolicy.SameHostSuccessor
                    && !MatchesHostIdentity(hostIdentitySession, session!))
                {
                    return DaemonSessionAcquisitionResult.Terminal(
                        DaemonSessionAcquisitionKind.HostIdentityMismatch);
                }

                if (hasWaitedSinceDelivery
                    || session!.SessionGenerationId != failedSession.SessionGenerationId)
                {
                    return DaemonSessionAcquisitionResult.Success(session!);
                }
            }
            else if (!readResult.IsSuccess)
            {
                return DaemonSessionAcquisitionResult.ReadFailed(readResult);
            }

            if (await recoveryWaiter.DelayIfRecoveringAsync(
                    unityProject,
                    failedSession,
                    requestDeadline,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                endpointAvailabilityDeadline = null;
                hasWaitedSinceDelivery = true;
                continue;
            }

            if (requestDeadline.IsExpired)
            {
                return RequestDeadlineExpired();
            }

            if (!await DelayForPublicationAsync(
                    requestDeadline,
                    endpointAvailabilityDeadline!,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                return GetEndpointAvailabilityExpiredResult();
            }

            hasWaitedSinceDelivery = true;
        }
    }

    private async ValueTask<DaemonSessionReadResult?> ReadOnceAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var operation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon session read could begin.",
                "Timed out while reading daemon session.",
                token => daemonSessionStore.ReadAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    token))
            .ConfigureAwait(false);
        return operation.IsSuccess ? operation.Value : null;
    }

    private async ValueTask<DaemonSessionAcquisitionResult> ResolveCurrentCoreAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var readResult = await ReadOnceAsync(unityProject, requestDeadline, cancellationToken).ConfigureAwait(false);
        if (readResult is null || requestDeadline.IsExpired)
        {
            return RequestDeadlineExpired();
        }

        if (TryGetAcceptedSession(readResult, out var session))
        {
            return DaemonSessionAcquisitionResult.Success(session!);
        }

        return !readResult.IsSuccess
            ? DaemonSessionAcquisitionResult.ReadFailed(readResult)
            : DaemonSessionAcquisitionResult.Terminal(DaemonSessionAcquisitionKind.SessionNotAvailable);
    }

    private bool TryGetAcceptedSession (
        DaemonSessionReadResult readResult,
        out DaemonSession? session)
    {
        session = null;
        if (!readResult.IsSuccess || !readResult.Exists)
        {
            return false;
        }

        var candidate = readResult.Session!;
        if (IsKnownRejected(candidate))
        {
            return false;
        }

        session = candidate;
        return true;
    }

    private bool IsKnownRejected (DaemonSession candidate)
    {
        return rejectedGenerationIds is not null
            && rejectedGenerationIds.Contains(candidate.SessionGenerationId);
    }

    private void RecordRejectedGeneration (DaemonSession rejectedSession)
    {
        rejectedGenerationIds ??= [];
        rejectedGenerationIds.Add(rejectedSession.SessionGenerationId);
    }

    private void ResetEndpointAvailabilityWindow ()
    {
        endpointAvailabilityGenerationId = null;
        endpointAvailabilityDeadline = null;
    }

    private static bool MatchesHostIdentity (
        DaemonSession interruptedSession,
        DaemonSession candidate)
    {
        if (candidate.ProjectFingerprint != interruptedSession.ProjectFingerprint
            || candidate.EditorMode != interruptedSession.EditorMode
            || candidate.ProcessId is not int candidateProcessId
            || interruptedSession.ProcessId is not int interruptedProcessId
            || candidateProcessId != interruptedProcessId
            || candidate.ProcessStartedAtUtc is not DateTimeOffset candidateProcessStartedAtUtc
            || interruptedSession.ProcessStartedAtUtc is not DateTimeOffset interruptedProcessStartedAtUtc
            || !DaemonProcessStartTimeMatcher.Matches(
                candidateProcessStartedAtUtc,
                interruptedProcessStartedAtUtc))
        {
            return false;
        }

        return interruptedSession.EditorMode switch
        {
            DaemonEditorMode.Batchmode => true,
            DaemonEditorMode.Gui => interruptedSession.EditorInstanceId is Guid interruptedEditorInstanceId
                && candidate.EditorInstanceId == interruptedEditorInstanceId,
            _ => false,
        };
    }

    private static async ValueTask<bool> DelayForPublicationAsync (
        ExecutionDeadline requestDeadline,
        ExecutionDeadline publicationDeadline,
        CancellationToken cancellationToken)
    {
        if (!requestDeadline.TryGetRemainingTimeout(out var requestRemainingTimeout)
            || !publicationDeadline.TryGetRemainingTimeout(out var publicationRemainingTimeout))
        {
            return false;
        }

        var remainingTimeout = requestRemainingTimeout < publicationRemainingTimeout
            ? requestRemainingTimeout
            : publicationRemainingTimeout;
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        await TimeProviderDelay.DelayAsync(
                TimeSpan.FromMilliseconds(retryDelayMilliseconds),
                requestDeadline.Clock,
                cancellationToken)
            .ConfigureAwait(false);
        return requestDeadline.TryGetRemainingTimeout(out _)
            && publicationDeadline.TryGetRemainingTimeout(out _);
    }

    private static DaemonSessionAcquisitionResult RequestDeadlineExpired ()
    {
        return DaemonSessionAcquisitionResult.Terminal(DaemonSessionAcquisitionKind.RequestDeadlineExpired);
    }

    private DaemonSessionAcquisitionResult GetPublicationExpiredResult ()
    {
        return requestDeadline.IsExpired
            ? RequestDeadlineExpired()
            : DaemonSessionAcquisitionResult.Terminal(DaemonSessionAcquisitionKind.PublicationWindowExpired);
    }

    private DaemonSessionAcquisitionResult GetEndpointAvailabilityExpiredResult ()
    {
        return requestDeadline.IsExpired
            ? RequestDeadlineExpired()
            : DaemonSessionAcquisitionResult.Terminal(DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired);
    }

    private enum SessionSuccessorPolicy
    {
        AnyHostSuccessor,
        SameHostSuccessor,
    }
}
