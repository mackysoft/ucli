using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonCompensationOperationOwnerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenMutationIgnoresCancellation_RetainsAdmissionUntilOwnedMutationQuiesces ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-owner"));
        var mutationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMutation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstDeadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(100), timeProvider);

        var firstExecutionTask = owner.ExecuteAsync<bool>(
                context,
                DaemonOperationLane.LifecycleCompensation,
                firstDeadline,
                CancellationToken.None,
                "Timed out before compensation began.",
                "Timed out while compensation remained owned.",
                async (_, _) =>
                {
                    mutationStarted.TrySetResult();
                    await releaseMutation.Task.ConfigureAwait(false);
                    throw new InvalidOperationException("deferred mutation failed");
                })
            .AsTask();
        Assert.True(mutationStarted.Task.IsCompletedSuccessfully);
        await mutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var firstResult = await firstExecutionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(firstResult.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, firstResult.Error!.Kind);
        var lifecycleLease = new RecordingAsyncDisposable();
        Assert.True(owner.TryTransferLifecycleLease(context, lifecycleLease));
        Assert.Equal(0, lifecycleLease.DisposeCount);

        var secondMutationInvoked = false;
        var secondDeadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50), timeProvider);
        var secondExecutionTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.LifecycleCompensation,
                secondDeadline,
                CancellationToken.None,
                "Timed out waiting for owned compensation.",
                "Timed out while second compensation was running.",
                (_, _) =>
                {
                    secondMutationInvoked = true;
                    return ValueTask.FromResult(true);
                })
            .AsTask();

        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var secondResult = await secondExecutionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(secondResult.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, secondResult.Error!.Kind);
        Assert.False(secondMutationInvoked);

        releaseMutation.TrySetResult();
        var quiescenceDeadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider);
        var quiescenceError = await owner.WaitForQuiescenceAsync(
                    context,
                    quiescenceDeadline,
                    CancellationToken.None,
                    "Timed out waiting for deferred compensation to quiesce.")
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(quiescenceError);
        Assert.Equal(1, lifecycleLease.DisposeCount);

        var thirdResult = await owner.ExecuteAsync(
            context,
            DaemonOperationLane.LifecycleCompensation,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
            CancellationToken.None,
            "Timed out before replacement compensation began.",
            "Timed out while replacement compensation was running.",
            (_, _) => ValueTask.FromResult(true));
        Assert.True(thirdResult.IsSuccess);
        Assert.True(thirdResult.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAnotherProjectOwnsCompensation_AdmitsIndependentProjectConcurrently ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var firstContext = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-owner-first"));
        var secondContext = ProjectContextTestFactory.CreateUnityProjectWithPaths(
            unityProjectRoot: ProjectPathTestValues.IndependentUnityProject,
            repositoryRoot: ProjectPathTestValues.TemporaryRepositoryRoot,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint-compensation-owner-second"),
            pathSourceLabel: null);
        var firstMutationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstMutation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstExecutionTask = owner.ExecuteAsync(
                firstContext,
                DaemonOperationLane.LifecycleCompensation,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                CancellationToken.None,
                "Timed out before first compensation began.",
                "Timed out while first compensation was running.",
                async (_, _) =>
                {
                    firstMutationStarted.TrySetResult();
                    await releaseFirstMutation.Task.ConfigureAwait(false);
                    return true;
                })
            .AsTask();
        await firstMutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondResult = await owner.ExecuteAsync(
                    secondContext,
                    DaemonOperationLane.LifecycleCompensation,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out before second compensation began.",
                    "Timed out while second compensation was running.",
                    (_, _) => ValueTask.FromResult(true))
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(secondResult.IsSuccess);
        releaseFirstMutation.TrySetResult();
        var firstResult = await firstExecutionTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(firstResult.IsSuccess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSupplementalLaneIsOccupied_AdmitsLifecycleLaneForSameProject ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-operation-lane-independence"));
        var supplementalStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSupplemental = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var supplementalTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.SupplementalPersistence,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                CancellationToken.None,
                "Timed out before supplemental persistence began.",
                "Timed out while supplemental persistence was running.",
                async (_, _) =>
                {
                    supplementalStarted.TrySetResult();
                    await releaseSupplemental.Task.ConfigureAwait(false);
                    return true;
                })
            .AsTask();

        try
        {
            await supplementalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var lifecycleResult = await owner.ExecuteAsync(
                        context,
                        DaemonOperationLane.LifecycleCompensation,
                        ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                        CancellationToken.None,
                        "Timed out before lifecycle compensation began.",
                        "Timed out while lifecycle compensation was running.",
                        (_, _) => ValueTask.FromResult(true))
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            var lifecycleQuiescenceError = await owner.WaitForQuiescenceAsync(
                context,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                CancellationToken.None,
                "Timed out waiting for lifecycle compensation.");
            var lifecycleLease = new RecordingAsyncDisposable();
            var lifecycleLeaseTransferred = owner.TryTransferLifecycleLease(
                context,
                lifecycleLease);

            Assert.True(lifecycleResult.IsSuccess);
            Assert.Null(lifecycleQuiescenceError);
            Assert.False(lifecycleLeaseTransferred);
            await lifecycleLease.DisposeAsync();
        }
        finally
        {
            releaseSupplemental.TrySetResult();
            var supplementalResult = await supplementalTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(supplementalResult.IsSuccess);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSameSupplementalLaneIsOccupied_SerializesLaterMutation ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-supplemental-lane-serialization"));
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInvoked = false;
        var firstTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.SupplementalPersistence,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                CancellationToken.None,
                "Timed out before first supplemental persistence began.",
                "Timed out while first supplemental persistence was running.",
                async (_, _) =>
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task.ConfigureAwait(false);
                    return true;
                })
            .AsTask();
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.SupplementalPersistence,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                CancellationToken.None,
                "Timed out waiting for first supplemental persistence.",
                "Timed out while second supplemental persistence was running.",
                (_, _) =>
                {
                    secondInvoked = true;
                    return ValueTask.FromResult(true);
                })
            .AsTask();

        try
        {
            Assert.False(secondTask.IsCompleted);
            Assert.False(secondInvoked);
        }
        finally
        {
            releaseFirst.TrySetResult();
        }

        var firstResult = await firstTask.WaitAsync(TimeSpan.FromSeconds(5));
        var secondResult = await secondTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.True(secondInvoked);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCancellationCallbackBlocks_ReturnsAtDeadlineAndRetainsOwnershipThroughCallback ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-blocking-callback"));
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var operationCancellationToken = CancellationToken.None;
        var executionTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.LifecycleCompensation,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(100), timeProvider),
                CancellationToken.None,
                "Timed out before callback compensation began.",
                "Timed out while callback compensation was running.",
                async (_, ownedCancellationToken) =>
                {
                    operationCancellationToken = ownedCancellationToken;
                    using var registration = ownedCancellationToken.Register(() =>
                    {
                        callbackStarted.TrySetResult();
                        releaseCallback.Task.GetAwaiter().GetResult();
                    });
                    operationStarted.TrySetResult();
                    await releaseOperation.Task.ConfigureAwait(false);
                    return true;
                })
            .AsTask();
        try
        {
            await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            timeProvider.Advance(TimeSpan.FromMilliseconds(100));
            var result = await executionTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.True(operationCancellationToken.IsCancellationRequested);
            await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            releaseOperation.TrySetResult();
            var admissionTask = owner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for blocking cancellation callback.")
                .AsTask();
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));
            var admissionError = await admissionTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ExecutionErrorKind.Timeout, admissionError!.Kind);

            releaseCallback.TrySetResult();
            var quiescenceError = await owner.WaitForQuiescenceAsync(
                        context,
                        ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                        CancellationToken.None,
                        "Timed out waiting for callback compensation quiescence.")
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(quiescenceError);
        }
        finally
        {
            releaseOperation.TrySetResult();
            releaseCallback.TrySetResult();
            _ = await owner.WaitForQuiescenceAsync(
                        context,
                        ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                        CancellationToken.None,
                        "Timed out cleaning up blocking callback compensation.")
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTransferredLifecycleLeaseCannotBeReleased_KeepsLaterAdmissionClosed ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-lease-release-failure"));
        var mutationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMutation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executionTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.LifecycleCompensation,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(100), timeProvider),
                CancellationToken.None,
                "Timed out before lease-failure compensation began.",
                "Timed out while lease-failure compensation was running.",
                async (_, _) =>
                {
                    mutationStarted.TrySetResult();
                    await releaseMutation.Task.ConfigureAwait(false);
                    return true;
                })
            .AsTask();
        await mutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await executionTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.IsSuccess);
        var lifecycleLease = new ThrowingAsyncDisposable();
        Assert.True(owner.TryTransferLifecycleLease(context, lifecycleLease));

        releaseMutation.TrySetResult();
        await lifecycleLease.DisposeAttempted.WaitAsync(TimeSpan.FromSeconds(5));
        var admissionTask = owner.WaitForQuiescenceAsync(
                context,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50), timeProvider),
                CancellationToken.None,
                "Timed out because lifecycle lease release failed.")
            .AsTask();
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var admissionError = await admissionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ExecutionErrorKind.Timeout, admissionError!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallerIsCanceled_ReturnsCancellationWithoutCancelingOwnedMutation ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-caller-cancellation"));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var mutationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMutation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var ownedTokenWasCanceled = true;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.ExecuteAsync(
                context,
                DaemonOperationLane.LifecycleCompensation,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                cancellationTokenSource.Token,
                "Timed out before canceled compensation began.",
                "Timed out while canceled compensation was running.",
                async (_, ownedCancellationToken) =>
                {
                    ownedTokenWasCanceled = ownedCancellationToken.IsCancellationRequested;
                    mutationStarted.TrySetResult();
                    await releaseMutation.Task.ConfigureAwait(false);
                    return true;
                })
            .AsTask());
        await mutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(ownedTokenWasCanceled);
        releaseMutation.TrySetResult();
        var quiescenceError = await owner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for caller-canceled mutation quiescence.")
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(quiescenceError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallerCancelsBeforeDeadline_RequestsOwnedCancellationAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-caller-cancellation-deadline"));
        using var cancellationTokenSource = new CancellationTokenSource();
        var mutationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var ownedCancellationObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleLease = new RecordingAsyncDisposable();
        var timeout = TimeSpan.FromMilliseconds(100);

        var executionTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.LifecycleCompensation,
                ExecutionDeadline.Start(timeout, timeProvider),
                cancellationTokenSource.Token,
                "Timed out before caller-canceled compensation began.",
                "Timed out while caller-canceled compensation was running.",
                async (_, ownedCancellationToken) =>
                {
                    mutationStarted.TrySetResult();
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, ownedCancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ownedCancellationToken.IsCancellationRequested)
                    {
                        ownedCancellationObserved.TrySetResult();
                        throw;
                    }

                    return true;
                })
            .AsTask();
        await mutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationTokenSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executionTask);
        Assert.False(ownedCancellationObserved.Task.IsCompleted);
        Assert.True(owner.TryTransferLifecycleLease(context, lifecycleLease));

        await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(timeout);
        await ownedCancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var quiescenceError = await owner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for deadline-canceled compensation.")
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(quiescenceError);
        Assert.Equal(1, lifecycleLease.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenOperationCancelsAtOwnedDeadline_ReturnsStructuredTimeout ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-deadline-cancellation-race"));
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var operationCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = TimeSpan.FromMilliseconds(100);
        using var operationDeadlineTimer = timeProvider.CreateTimer(
            _ => operationCompletion.TrySetCanceled(),
            state: null,
            timeout,
            Timeout.InfiniteTimeSpan);

        var executionTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.LifecycleCompensation,
                ExecutionDeadline.Start(timeout, timeProvider),
                CancellationToken.None,
                "Timed out before deadline-racing compensation began.",
                "Timed out while deadline-racing compensation was running.",
                (_, _) =>
                {
                    operationStarted.TrySetResult();
                    return new ValueTask<bool>(operationCompletion.Task);
                })
            .AsTask();
        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        timeProvider.Advance(timeout);
        var result = await executionTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitForQuiescence_WhenReleaseAndCallerCancellationCoincide_PrefersCallerCancellation ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var owner = new DaemonCompensationOperationOwner();
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            ProjectFingerprintTestFactory.Create("fingerprint-compensation-quiescence-cancellation-race"));
        var operationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var operationTimeout = TimeSpan.FromMilliseconds(100);
        var executionTask = owner.ExecuteAsync(
                context,
                DaemonOperationLane.LifecycleCompensation,
                ExecutionDeadline.Start(operationTimeout, timeProvider),
                CancellationToken.None,
                "Timed out before quiescence-racing compensation began.",
                "Timed out while quiescence-racing compensation was running.",
                async (_, _) =>
                {
                    operationStarted.TrySetResult();
                    await releaseOperation.Task.ConfigureAwait(false);
                    return true;
                })
            .AsTask();
        await operationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        timeProvider.Advance(operationTimeout);
        var executionResult = await executionTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(executionResult.IsSuccess);

        using var cancellationTokenSource = new CancellationTokenSource();
        Assert.True(owner.TryTransferLifecycleLease(
            context,
            new CancelingAsyncDisposable(cancellationTokenSource)));
        var quiescenceTask = owner.WaitForQuiescenceAsync(
                context,
                ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                cancellationTokenSource.Token,
                "Timed out waiting for quiescence-racing compensation.")
            .AsTask();

        releaseOperation.TrySetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => quiescenceTask.WaitAsync(TimeSpan.FromSeconds(5)));
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

    private sealed class ThrowingAsyncDisposable : IAsyncDisposable
    {
        private readonly TaskCompletionSource disposeAttempted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task DisposeAttempted => disposeAttempted.Task;

        public ValueTask DisposeAsync ()
        {
            disposeAttempted.TrySetResult();
            return ValueTask.FromException(new IOException("lifecycle lease release failed"));
        }
    }

    private sealed class CancelingAsyncDisposable : IAsyncDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource;

        public CancelingAsyncDisposable (CancellationTokenSource cancellationTokenSource)
        {
            this.cancellationTokenSource = cancellationTokenSource;
        }

        public ValueTask DisposeAsync ()
        {
            cancellationTokenSource.Cancel();
            return ValueTask.CompletedTask;
        }
    }
}
