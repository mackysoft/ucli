using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenObservedSessionResponds_ReturnsSameSessionAndPing ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-probe-current"));
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "current-token");
        var pingResponse = IpcUnityEditorObservationTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint);
        var pingInfoClient = new RecordingDaemonPingInfoClient(pingResponse);
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(new RecordingDaemonSessionStore()),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var result = await probe.ProbeAsync(
            unityProject,
            session,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        Assert.Same(pingResponse, result.PingResponse);
        Assert.Null(result.SessionReadFailure);
        Assert.Equal(session, Assert.Single(pingInfoClient.Invocations).Session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenReplacementPublicationIsDelayed_PingsReplacementOnceWithSameRequestId ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-probe-rotation"));
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var replacementSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "replacement-token",
            issuedAtUtc: observedSession.IssuedAtUtc.AddSeconds(1),
            sessionGenerationId: Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count switch
            {
                1 => DaemonSessionReadResultTestFactory.Found(observedSession),
                2 => DaemonSessionReadResult.Missing(),
                _ => DaemonSessionReadResultTestFactory.Found(replacementSession),
            },
        };
        var replacementPing = IpcUnityEditorObservationTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint);
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "Session token rotated.",
                IpcSessionErrorCodes.SessionTokenInvalid),
            replacementPing);
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var probeTask = probe.ProbeAsync(
                unityProject,
                observedSession,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await AdvanceNextPublicationRetryAsync(timeProvider);
        await AdvanceNextPublicationRetryAsync(timeProvider);

        var result = await probeTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(replacementSession, result.Session);
        Assert.Same(replacementPing, result.PingResponse);
        Assert.Equal(3, sessionStore.ReadInvocations.Count);
        Assert.Collection(
            pingInfoClient.Invocations,
            invocation => Assert.Equal(observedSession, invocation.Session),
            invocation => Assert.Equal(replacementSession, invocation.Session));
        var requestId = pingInfoClient.Invocations[0].RequestId;
        Assert.NotNull(requestId);
        Assert.NotEqual(Guid.Empty, requestId.Value);
        Assert.Equal(requestId, pingInfoClient.Invocations[1].RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenReplacementPublicationExhaustsRequestDeadline_ReturnsTimeoutWithoutSecondPing ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-probe-publication-timeout"));
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var tokenRejection = new DaemonPingResponseException(
            "Session token rotated.",
            IpcSessionErrorCodes.SessionTokenInvalid);
        var pingInfoClient = new RecordingDaemonPingInfoClient(tokenRejection);
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(observedSession))),
            pingInfoClient,
            new DaemonReachabilityClassifier());
        var timeout = TimeSpan.FromMilliseconds(150);

        var probeTask = probe.ProbeAsync(
                unityProject,
                observedSession,
                ExecutionDeadline.Start(timeout, timeProvider),
                CancellationToken.None)
            .AsTask();
        await AdvanceNextPublicationRetryAsync(timeProvider);
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50));
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        var result = await probeTask;

        Assert.False(result.IsSuccess);
        Assert.IsType<TimeoutException>(result.ProbeFailure);
        Assert.Single(pingInfoClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenCanceledDuringReplacementPublicationWait_ThrowsWithoutSecondPing ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-probe-publication-canceled"));
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "Session token rotated.",
                IpcSessionErrorCodes.SessionTokenInvalid));
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing())),
            pingInfoClient,
            new DaemonReachabilityClassifier());
        using var cancellationTokenSource = new CancellationTokenSource();

        var probeTask = probe.ProbeAsync(
                unityProject,
                observedSession,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                cancellationTokenSource.Token)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(100));

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probeTask);
        Assert.Single(pingInfoClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenReplacementSessionReadIsInvalid_ReturnsReadFailureWithoutSecondPing ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-probe-invalid-replacement"));
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var readFailure = DaemonSessionReadResultTestFactory.Invalid(
            artifactIdentity: DaemonSessionArtifactIdentity.Create(
                System.Text.Encoding.UTF8.GetBytes("{ invalid")));
        var sessionStore = new RecordingDaemonSessionStore(readFailure);
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "Session token rotated.",
                IpcSessionErrorCodes.SessionTokenInvalid));
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var result = await probe.ProbeAsync(
            unityProject,
            observedSession,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(readFailure, result.SessionReadFailure);
        Assert.Equal(observedSession, result.Session);
        Assert.Null(result.PingResponse);
        Assert.Null(result.ProbeFailure);
        Assert.Single(sessionStore.ReadInvocations);
        Assert.Single(pingInfoClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenReplacementProbeFails_ReturnsFailureWithReplacementSession ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-probe-replacement-failure"));
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var replacementSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "replacement-token",
            issuedAtUtc: observedSession.IssuedAtUtc.AddSeconds(1),
            sessionGenerationId: Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var replacementFailure = new InvalidOperationException("Replacement probe failed.");
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "Session token rotated.",
                IpcSessionErrorCodes.SessionTokenInvalid),
            replacementFailure);
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(replacementSession))),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var result = await probe.ProbeAsync(
            unityProject,
            observedSession,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(replacementSession, result.Session);
        Assert.Same(replacementFailure, result.ProbeFailure);
        Assert.Null(result.PingResponse);
        Assert.Null(result.SessionReadFailure);
        Assert.Collection(
            pingInfoClient.Invocations,
            invocation => Assert.Equal(observedSession, invocation.Session),
            invocation => Assert.Equal(replacementSession, invocation.Session));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenResponseInterruptionOutlivesEndpointWindow_PreservesInterruptionFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-probe-response-interruption"));
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var interruption = new IpcResponseReadInterruptedException(
            new IOException("The daemon closed the response stream."));
        var pingInfoClient = new RecordingDaemonPingInfoClient
        {
            PingSessionAndReadHandler = (_, _, _, _, _) => throw interruption,
        };
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing())),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var probeTask = probe.ProbeAsync(
                unityProject,
                observedSession,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await AdvanceNextPublicationRetryAsync(timeProvider);
        }

        var result = await probeTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.Same(interruption, result.ProbeFailure);
        Assert.Equal(observedSession, result.Session);
        Assert.Null(result.SessionReadFailure);
        Assert.Single(pingInfoClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenTimeAdvancesBeforeSuccessorDelivery_PreservesRequestIdentityAndDeadline ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint"));
        var observedSession = DaemonSessionTestFactory.CreateForToken(
            "observed-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-observed-endpoint");
        var successorSession = DaemonSessionTestFactory.CreateForToken(
            "successor-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-successor-endpoint");
        var transportClient = new RecordingIpcTransportClient(request =>
            IpcResponseTestFactory.CreateSuccess(
                request,
                IpcUnityEditorObservationTestFactory.Create(
                    projectFingerprint: unityProject.ProjectFingerprint)));
        transportClient.EnqueueResponse(_ =>
        {
            timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap);
            throw new IpcConnectTimeoutException("The observed endpoint did not accept the first delivery.");
        });
        var exactSessionPingClient = new IpcDaemonPingClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new UnexpectedDaemonSessionStore("Exact-session ping must not read session metadata.")),
            timeProvider);
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(successorSession))),
            exactSessionPingClient,
            new DaemonReachabilityClassifier());
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(3), timeProvider);

        var result = await probe.ProbeAsync(
            unityProject,
            observedSession,
            deadline,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(successorSession, result.Session);
        IpcRequestAssert.SessionTokens(
            transportClient.Requests,
            observedSession.SessionToken.GetEncodedValue(),
            successorSession.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(transportClient.Requests);
        Assert.All(
            transportClient.Requests,
            request => Assert.Equal(deadline.UtcDeadline, request.RequestDeadlineUtc));
        Assert.True(
            transportClient.Requests[0].RequestDeadlineRemainingMilliseconds
            > transportClient.Requests[1].RequestDeadlineRemainingMilliseconds);
        Assert.All(
            transportClient.Timeouts,
            timeout => Assert.InRange(
                timeout,
                TimeSpan.FromTicks(1),
                DaemonTimeouts.ProbeAttemptTimeoutCap));
        Assert.Equal(startedAtUtc + DaemonTimeouts.ProbeAttemptTimeoutCap, timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenRefreshedMetadataKeepsRejectedGeneration_DoesNotRetrySameGeneration ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            ProjectFingerprintTestFactory.Create("fingerprint-session-probe-same-token"));
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "rejected-token");
        var refreshedMetadata = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "rejected-token",
            issuedAtUtc: observedSession.IssuedAtUtc.AddSeconds(1),
            endpointAddress: "updated-endpoint");
        var tokenRejection = new DaemonPingResponseException(
            "Session token was rejected.",
            IpcSessionErrorCodes.SessionTokenInvalid);
        var pingInfoClient = new RecordingDaemonPingInfoClient(tokenRejection);
        var sessionStore = new RecordingDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(refreshedMetadata));
        var probe = new DaemonSessionProbe(
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var probeTask = probe.ProbeAsync(
                unityProject,
                observedSession,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            probeTask,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await probeTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(observedSession, result.Session);
        Assert.Same(tokenRejection, result.ProbeFailure);
        Assert.Single(pingInfoClient.Invocations);
    }

    private static async Task AdvanceNextPublicationRetryAsync (ManualTimeProvider timeProvider)
    {
        var retryDelay = TimeSpan.FromMilliseconds(100);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay);
        timeProvider.Advance(retryDelay);
    }
}
