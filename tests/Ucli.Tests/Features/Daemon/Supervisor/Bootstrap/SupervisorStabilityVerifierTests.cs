using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorStabilityVerifierTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenRemainingTimeoutIsExhausted_ReturnsTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingClient = new RecordingDaemonPingClient((_, _, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            return ValueTask.CompletedTask;
        });
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore),
            new DaemonCompensationOperationOwner(),
            timeProvider);
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: "fingerprint");
        var session = DaemonSessionTestFactory.Create(sessionToken: "session-token");

        var result = await verifier.EnsureStableAsync(
            unityProject,
            session,
            TimeSpan.FromMilliseconds(180),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
        var diagnosis = DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, unityProject);
        Assert.Equal(DaemonDiagnosisReasonValues.StartupUnstable, diagnosis.Reason);
        Assert.Equal("Unity daemon stability verification exceeded the remaining timeout.", diagnosis.Message);
        DaemonPingClientAssert.StabilityVerificationAttemptedBeforeRemainingTimeoutExhausted(pingClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenSuccessfulPingsCompleteWithinBudget_UsesCommandTimeoutBudget ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingClient = new RecordingDaemonPingClient((_, timeout, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(TimeSpan.FromSeconds(1), timeout);
            return ValueTask.CompletedTask;
        });
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(new RecordingDaemonDiagnosisStore()),
            new DaemonCompensationOperationOwner(),
            timeProvider);

        var verificationTask = verifier.EnsureStableAsync(
                ResolvedUnityProjectContextTestFactory.Create(
                    unityProjectRoot: "/tmp/unity-project",
                    repositoryRoot: "/tmp/repo-root",
                    projectFingerprint: "fingerprint"),
                DaemonSessionTestFactory.Create(sessionToken: "session-token"),
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        for (var i = 0; i < 8 && !verificationTask.IsCompleted; i++)
        {
            await ManualTimeTaskDriver.WaitForTimerDueWithinOrCompletionAsync(
                timeProvider,
                verificationTask,
                TimeSpan.FromMilliseconds(700));
            timeProvider.Advance(TimeSpan.FromMilliseconds(700));
        }

        var result = await TestAwaiter.WaitAsync(
            verificationTask,
            "Supervisor stability verification",
            TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        DaemonPingClientAssert.StabilityVerificationPingsUsedCommandTimeoutBudget(
            pingClient,
            TimeSpan.FromSeconds(1),
            expectedPingCount: 3);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenPingFails_ReturnsFailureWithoutCompensationStop ()
    {
        var pingClient = new RecordingDaemonPingClient(static (_, _, _, _) =>
            ValueTask.FromException(new InvalidOperationException("ping failed")));
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: "fingerprint");
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore),
            new DaemonCompensationOperationOwner(),
            TimeProvider.System);

        var result = await verifier.EnsureStableAsync(
            unityProject,
            DaemonSessionTestFactory.Create(sessionToken: "session-token"),
            TimeSpan.FromMilliseconds(400),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("Unity daemon failed the supervisor stability window. ping failed", result.Error.Message, StringComparison.Ordinal);
        DaemonDiagnosisStoreAssert.DiagnosisWrittenFor(diagnosisStore, unityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenDiagnosisWriteFails_ReturnsAugmentedFailure ()
    {
        var pingClient = new RecordingDaemonPingClient(static (_, _, _, _) =>
            ValueTask.FromException(new InvalidOperationException("ping failed")));
        var diagnosisStore = new RecordingDaemonDiagnosisStore
        {
            WriteResult = DaemonDiagnosisStoreOperationResult.Failure(
                ExecutionError.InternalError("diagnosis failed")),
        };
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore),
            new DaemonCompensationOperationOwner(),
            TimeProvider.System);

        var result = await verifier.EnsureStableAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: "/tmp/repo-root",
                projectFingerprint: "fingerprint"),
            DaemonSessionTestFactory.Create(sessionToken: "session-token"),
            TimeSpan.FromMilliseconds(400),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("DiagnosisError=diagnosis failed", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStable_WhenDiagnosisWriteDoesNotComplete_OwnsSupplementalLaneWithoutBlockingLifecycleLane ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingClient = new RecordingDaemonPingClient((_, _, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            return ValueTask.CompletedTask;
        });
        var diagnosisStore = new NonCooperativeDiagnosisStore();
        var compensationOwner = new DaemonCompensationOperationOwner();
        var verifier = new SupervisorStabilityVerifier(
            pingClient,
            new SupervisorDiagnosisWriter(diagnosisStore),
            compensationOwner,
            timeProvider);
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repo-root",
            projectFingerprint: "fingerprint-non-cooperative-diagnosis");
        var verificationTask = verifier.EnsureStableAsync(
                unityProject,
                DaemonSessionTestFactory.Create(sessionToken: "session-token"),
                TimeSpan.FromMilliseconds(180),
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(
            diagnosisStore.WriteStarted,
            "Non-cooperative stability diagnosis write",
            SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        SupervisorStabilityVerificationResult result;
        ExecutionError? lifecycleQuiescenceError;
        ExecutionDeadlineOperationResult<bool> supplementalAdmissionResult;
        var replacementSupplementalInvoked = false;
        try
        {
            result = await verificationTask.WaitAsync(TimeSpan.FromMilliseconds(250));
            lifecycleQuiescenceError = await compensationOwner.WaitForQuiescenceAsync(
                unityProject,
                ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50), timeProvider),
                CancellationToken.None,
                "Timed out waiting for lifecycle compensation.");
            var supplementalAdmissionTask = compensationOwner.ExecuteAsync(
                    unityProject,
                    DaemonOperationLane.SupplementalPersistence,
                    ExecutionDeadline.Start(TimeSpan.FromMilliseconds(50), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for owned stability diagnosis write.",
                    "Timed out while replacement supplemental persistence was running.",
                    (_, _) =>
                    {
                        replacementSupplementalInvoked = true;
                        return ValueTask.FromResult(true);
                    })
                .AsTask();
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));
            supplementalAdmissionResult = await supplementalAdmissionTask.WaitAsync(SignalWaitTimeout);
        }
        finally
        {
            diagnosisStore.CompleteWrite();
            _ = await verificationTask.WaitAsync(SignalWaitTimeout);
        }

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Contains("DiagnosisError=", result.Error.Message, StringComparison.Ordinal);
        Assert.Null(lifecycleQuiescenceError);
        Assert.False(supplementalAdmissionResult.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, supplementalAdmissionResult.Error!.Kind);
        Assert.False(replacementSupplementalInvoked);

        var replacementResult = await compensationOwner.ExecuteAsync(
            unityProject,
            DaemonOperationLane.SupplementalPersistence,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
            CancellationToken.None,
            "Timed out waiting for released stability diagnosis ownership.",
            "Timed out while replacement supplemental persistence was running.",
            (_, _) => ValueTask.FromResult(true));
        Assert.True(replacementResult.IsSuccess);
    }

    private sealed class NonCooperativeDiagnosisStore : IDaemonDiagnosisStore
    {
        private readonly TaskCompletionSource writeStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<DaemonDiagnosisStoreOperationResult> writeCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WriteStarted => writeStarted.Task;

        public void CompleteWrite ()
        {
            writeCompletion.TrySetResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<DaemonDiagnosisReadResult>(
                new InvalidOperationException("Diagnosis read is not expected."));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            writeStarted.TrySetResult();
            return new ValueTask<DaemonDiagnosisStoreOperationResult>(writeCompletion.Task);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<DaemonDiagnosisStoreOperationResult>(
                new InvalidOperationException("Diagnosis deletion is not expected."));
        }
    }

}
