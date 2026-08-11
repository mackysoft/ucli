using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStartOperationTestSupport;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartOperationCompensationOwnershipTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenLaunchLeavesOwnedCompensation_RetainsLifecycleLeaseUntilLaunchCompensationQuiesces ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var lifecycleLease = new RecordingAsyncDisposable();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-start-launch-owned-compensation"));
        var compensationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCompensation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var launchService = new RecordingDaemonLaunchService
        {
            Handler = async (unityProject, _, _, _, _, _) =>
            {
                var executionResult = await compensationOperationOwner.ExecuteAsync<bool>(
                    unityProject,
                    DaemonOperationLane.LifecycleCompensation,
                    ExecutionDeadline.Start(TimeSpan.FromMilliseconds(100), timeProvider),
                    CancellationToken.None,
                    "Timed out before launch compensation began.",
                    "Timed out while launch compensation remained owned.",
                    async (_, _) =>
                    {
                        compensationStarted.TrySetResult();
                        await releaseCompensation.Task.ConfigureAwait(false);
                        return true;
                    });
                return DaemonStartResult.Failure(executionResult.Error!);
            },
        };
        var operation = CreateOperation(
            daemonSessionStore: new RecordingDaemonSessionStore(
                DaemonSessionReadResult.Missing()),
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(
                (_, _, _) => lifecycleLease),
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var startTask = operation.StartAsync(
                context,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
                editorMode: UnityEditorMode.Batchmode,
                onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await compensationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await startTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Equal(0, lifecycleLease.DisposeCount);

        releaseCompensation.TrySetResult();
        var quiescenceError = await compensationOperationOwner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for launch compensation quiescence.")
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(quiescenceError);
        Assert.Equal(1, lifecycleLease.DisposeCount);
    }

    private sealed class RecordingAsyncDisposable : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync ()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
