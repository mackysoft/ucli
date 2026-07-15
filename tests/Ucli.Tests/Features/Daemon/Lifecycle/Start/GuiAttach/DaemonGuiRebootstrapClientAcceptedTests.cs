namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Daemon.DaemonGuiRebootstrapClientTestSupport;

public sealed class DaemonGuiRebootstrapClientAcceptedTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenSessionTokenRotates_ReloadsManifestAndReplaysSameRequestOnce ()
    {
        var manifest = CreateManifest();
        var initialManifest = new GuiSupervisorManifestJsonContract(
            manifest.SchemaVersion,
            IpcSessionTokenTestFactory.Create("initial-token"),
            manifest.ProjectFingerprint,
            manifest.Endpoint,
            manifest.ProcessId,
            manifest.ProcessStartedAtUtc,
            manifest.IssuedAtUtc);
        var successorManifest = new GuiSupervisorManifestJsonContract(
            initialManifest.SchemaVersion,
            IpcSessionTokenTestFactory.Create("successor-token"),
            initialManifest.ProjectFingerprint,
            initialManifest.Endpoint,
            initialManifest.ProcessId,
            initialManifest.ProcessStartedAtUtc,
            initialManifest.IssuedAtUtc.AddSeconds(1));
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            projectFingerprint: initialManifest.ProjectFingerprint);
        var manifestReadCount = 0;
        var manifestStore = new StubGuiSupervisorManifestStore(_ =>
            ValueTask.FromResult<GuiSupervisorManifestJsonContract?>(
                Interlocked.Increment(ref manifestReadCount) == 1
                    ? initialManifest
                    : successorManifest));
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(
                    string.Equals(
                        request.SessionToken,
                        initialManifest.SessionToken.GetEncodedValue(),
                        StringComparison.Ordinal)
                        ? IpcResponseTestFactory.CreateError(
                            request,
                            IpcSessionErrorCodes.SessionTokenInvalid,
                            "Initial GUI supervisor session token is invalid.")
                        : IpcResponseTestFactory.CreateSuccess(
                            request,
                            new IpcGuiRebootstrapResponse(
                                Accepted: true,
                                ProjectFingerprint: unityProject.ProjectFingerprint,
                                ProcessId: successorManifest.ProcessId)));
            },
        };
        var client = new DaemonGuiRebootstrapClient(
            manifestStore,
            transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            initialManifest.ProcessId,
            ProcessStartedAtUtc,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsAccepted);
        Assert.Equal(2, manifestReadCount);
        var requests = transportClient.Invocations.Select(static invocation => invocation.Request).ToArray();
        IpcRequestAssert.SessionTokens(
            requests,
            initialManifest.SessionToken.GetEncodedValue(),
            successorManifest.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenRejectedSessionTokenIsStillPublished_DoesNotReplayRequest ()
    {
        var manifest = CreateManifest();
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(projectFingerprint: manifest.ProjectFingerprint);
        var manifestReadCount = 0;
        var manifestStore = new StubGuiSupervisorManifestStore(_ =>
        {
            Interlocked.Increment(ref manifestReadCount);
            return ValueTask.FromResult<GuiSupervisorManifestJsonContract?>(manifest);
        });
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "GUI supervisor session token is invalid."));
            },
        };
        var client = new DaemonGuiRebootstrapClient(
            manifestStore,
            transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(2, manifestReadCount);
        Assert.Single(transportClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenSuccessorTokenIsAlsoRejected_DoesNotObserveThirdGeneration ()
    {
        var manifest = CreateManifest();
        var initialManifest = new GuiSupervisorManifestJsonContract(
            manifest.SchemaVersion,
            IpcSessionTokenTestFactory.Create("initial-token"),
            manifest.ProjectFingerprint,
            manifest.Endpoint,
            manifest.ProcessId,
            manifest.ProcessStartedAtUtc,
            manifest.IssuedAtUtc);
        var successorManifest = new GuiSupervisorManifestJsonContract(
            initialManifest.SchemaVersion,
            IpcSessionTokenTestFactory.Create("successor-token"),
            initialManifest.ProjectFingerprint,
            initialManifest.Endpoint,
            initialManifest.ProcessId,
            initialManifest.ProcessStartedAtUtc,
            initialManifest.IssuedAtUtc.AddSeconds(1));
        var unexpectedThirdManifest = new GuiSupervisorManifestJsonContract(
            successorManifest.SchemaVersion,
            IpcSessionTokenTestFactory.Create("unexpected-third-token"),
            successorManifest.ProjectFingerprint,
            successorManifest.Endpoint,
            successorManifest.ProcessId,
            successorManifest.ProcessStartedAtUtc,
            successorManifest.IssuedAtUtc.AddSeconds(1));
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            projectFingerprint: initialManifest.ProjectFingerprint);
        var manifestReadCount = 0;
        var manifestStore = new StubGuiSupervisorManifestStore(_ =>
            ValueTask.FromResult<GuiSupervisorManifestJsonContract?>(
                Interlocked.Increment(ref manifestReadCount) switch
                {
                    1 => initialManifest,
                    2 => successorManifest,
                    _ => unexpectedThirdManifest,
                }));
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, request, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "GUI supervisor session token is invalid."));
            },
        };
        var client = new DaemonGuiRebootstrapClient(
            manifestStore,
            transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            initialManifest.ProcessId,
            ProcessStartedAtUtc,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(2, manifestReadCount);
        var requests = transportClient.Invocations.Select(static invocation => invocation.Request).ToArray();
        IpcRequestAssert.SessionTokens(
            requests,
            initialManifest.SessionToken.GetEncodedValue(),
            successorManifest.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenManifestReadConsumesBudget_UsesRemainingTimeoutForIpc ()
    {
        var timeProvider = new ManualTimeProvider();
        var manifest = CreateManifest();
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(projectFingerprint: manifest.ProjectFingerprint);
        var manifestStore = new StubGuiSupervisorManifestStore(cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(400));
            return ValueTask.FromResult<GuiSupervisorManifestJsonContract?>(manifest);
        });
        var transportClient = CreateAcceptingTransport(unityProject.ProjectFingerprint, manifest);
        var client = new DaemonGuiRebootstrapClient(manifestStore, transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            CancellationToken.None);

        Assert.True(result.IsAccepted);
        var invocation = DaemonGuiRebootstrapTransportAssert.RebootstrapRequestedForManifest(
            transportClient,
            manifest,
            unityProject.ProjectFingerprint,
            TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.FromMilliseconds(100), invocation.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenManifestReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCompletion = new TaskCompletionSource<GuiSupervisorManifestJsonContract?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var manifest = CreateManifest();
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(projectFingerprint: manifest.ProjectFingerprint);
        var manifestStore = new StubGuiSupervisorManifestStore(_ =>
        {
            readStarted.TrySetResult();
            return new ValueTask<GuiSupervisorManifestJsonContract?>(readCompletion.Task);
        });
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, _, _, _) => throw new InvalidOperationException(
                "A timed-out manifest read must not send a GUI rebootstrap request."),
        };
        var client = new DaemonGuiRebootstrapClient(manifestStore, transportClient);
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = client.RequestRebootstrapAsync(
                unityProject,
                manifest.ProcessId,
                ProcessStartedAtUtc,
                ExecutionDeadline.Start(timeout, timeProvider),
                CancellationToken.None)
            .AsTask();
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsAccepted);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            DaemonGuiRebootstrapTransportAssert.NoIpcRequestWasSent(transportClient);
        }
        finally
        {
            readCompletion.TrySetResult(null);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenTransportIgnoresCancellation_ReturnsAtSharedDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sendCompletion = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var manifest = CreateManifest();
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(projectFingerprint: manifest.ProjectFingerprint);
        var manifestStore = new StubGuiSupervisorManifestStore(
            _ => ValueTask.FromResult<GuiSupervisorManifestJsonContract?>(manifest));
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, _, _, _) =>
            {
                sendStarted.TrySetResult();
                return new ValueTask<IpcResponse>(sendCompletion.Task);
            },
        };
        var client = new DaemonGuiRebootstrapClient(manifestStore, transportClient);
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = client.RequestRebootstrapAsync(
                unityProject,
                manifest.ProcessId,
                ProcessStartedAtUtc,
                ExecutionDeadline.Start(timeout, timeProvider),
                CancellationToken.None)
            .AsTask();
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsAccepted);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        }
        finally
        {
            sendCompletion.TrySetException(new TimeoutException("Release non-cooperative rebootstrap transport."));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenManifestMatchesAndSupervisorAccepts_ReturnsAccepted ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenManifestMatchesAndSupervisorAccepts_ReturnsAccepted));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var transportClient = CreateAcceptingTransport(unityProject.ProjectFingerprint, manifest);
        var client = CreateClient(transportClient);

        var timeout = TimeSpan.FromMilliseconds(500);
        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            ExecutionDeadline.Start(timeout, TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsAccepted);
        DaemonGuiRebootstrapTransportAssert.RebootstrapRequestedForManifest(
            transportClient,
            manifest,
            unityProject.ProjectFingerprint,
            timeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenManifestStartTimeDiffersWithinTolerance_RequestsSupervisor ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenManifestStartTimeDiffersWithinTolerance_RequestsSupervisor));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        var baseManifest = CreateManifest();
        var manifest = new GuiSupervisorManifestJsonContract(
            baseManifest.SchemaVersion,
            baseManifest.SessionToken,
            baseManifest.ProjectFingerprint,
            baseManifest.Endpoint,
            baseManifest.ProcessId,
            ProcessStartedAtUtc.AddMilliseconds(1),
            baseManifest.IssuedAtUtc);
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var timeout = TimeSpan.FromMilliseconds(500);
        var transportClient = CreateAcceptingTransport(unityProject.ProjectFingerprint, manifest);
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            ExecutionDeadline.Start(timeout, TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsAccepted);
        DaemonGuiRebootstrapTransportAssert.RebootstrapRequestedForManifest(
            transportClient,
            manifest,
            unityProject.ProjectFingerprint,
            timeout);
    }

    private static StubIpcTransportClient CreateAcceptingTransport (
        ProjectFingerprint projectFingerprint,
        GuiSupervisorManifestJsonContract manifest)
    {
        return new StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) => ValueTask.FromResult(IpcResponseTestFactory.CreateSuccess(
                request,
                new IpcGuiRebootstrapResponse(
                    Accepted: true,
                    ProjectFingerprint: projectFingerprint,
                    ProcessId: manifest.ProcessId))),
        };
    }

    private sealed class StubGuiSupervisorManifestStore : IGuiSupervisorManifestStore
    {
        private readonly Func<CancellationToken, ValueTask<GuiSupervisorManifestJsonContract?>> read;

        public StubGuiSupervisorManifestStore (
            Func<CancellationToken, ValueTask<GuiSupervisorManifestJsonContract?>> read)
        {
            this.read = read ?? throw new ArgumentNullException(nameof(read));
        }

        public ValueTask<GuiSupervisorManifestJsonContract?> ReadAfterEndpointPublicationAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
            ArgumentNullException.ThrowIfNull(projectFingerprint);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
            return read(cancellationToken);
        }
    }
}
