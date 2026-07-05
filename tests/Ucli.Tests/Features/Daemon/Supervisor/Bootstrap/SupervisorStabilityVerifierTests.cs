using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
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
            new SupervisorDiagnosisWriter(diagnosisStore));

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
            new SupervisorDiagnosisWriter(diagnosisStore));

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

}
