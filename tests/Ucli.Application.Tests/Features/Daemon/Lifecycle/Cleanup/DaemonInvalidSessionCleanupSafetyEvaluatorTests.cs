using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonInvalidSessionCleanupSafetyEvaluatorTests
{
    private static readonly DateTimeOffset ProcessStartedAtUtc =
        new(2026, 7, 13, 0, 0, 1, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenEvidenceIsNull_ReturnsFalse ()
    {
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor());

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-null"),
            null);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenFingerprintDoesNotMatch_ReturnsFalseWithoutIdentityAssessment ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-current");
        var evidence = CreateEvidence("fingerprint-other");
        var assessor = new RecordingDaemonProcessIdentityAssessor();
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, evidence);

        Assert.False(requiresUnsafeSkip);
        Assert.Empty(assessor.Invocations);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RequiresUnsafeSkip_WhenProcessIdIsNotPositive_ReturnsFalse (int? processId)
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-current");
        var evidence = CreateEvidence(context.ProjectFingerprint, processId);
        var assessor = new RecordingDaemonProcessIdentityAssessor();
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, evidence);

        Assert.False(requiresUnsafeSkip);
        Assert.Empty(assessor.Invocations);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(nameof(DaemonProcessIdentityAssessmentStatus.NotRunning), false)]
    [InlineData(nameof(DaemonProcessIdentityAssessmentStatus.DifferentProcess), false)]
    [InlineData(nameof(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess), true)]
    [InlineData(nameof(DaemonProcessIdentityAssessmentStatus.Uncertain), true)]
    public void RequiresUnsafeSkip_WhenProcessIdentityIsAssessed_ReturnsConservativeDecision (
        string statusName,
        bool expected)
    {
        var status = Enum.Parse<DaemonProcessIdentityAssessmentStatus>(statusName);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-current");
        var evidence = CreateEvidence(context.ProjectFingerprint);
        var assessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                status,
                ObservedStartTimeUtc: null,
                status is DaemonProcessIdentityAssessmentStatus.DifferentProcess or DaemonProcessIdentityAssessmentStatus.Uncertain
                    ? ExecutionError.InternalError("identity assessment did not prove a matching stopped process")
                    : null),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, evidence);

        Assert.Equal(expected, requiresUnsafeSkip);
        var invocation = Assert.Single(assessor.Invocations);
        Assert.Equal(1234, invocation.ProcessId);
        Assert.Equal(ProcessStartedAtUtc, invocation.ExpectedProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Evidence_DoesNotExposeSessionToken ()
    {
        Assert.Null(typeof(DaemonInvalidSessionEvidence).GetProperty("SessionToken"));
    }

    private static DaemonInvalidSessionEvidence CreateEvidence (
        string projectFingerprint,
        int? processId = 1234)
    {
        var contract = new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionToken: "raw-secret-must-not-be-projected",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: default,
            EditorMode: "invalid-editor-mode",
            OwnerKind: "invalid-owner-kind",
            CanShutdownProcess: false,
            EndpointTransportKind: null,
            EndpointAddress: null,
            ProcessId: processId,
            ProcessStartedAtUtc: ProcessStartedAtUtc,
            OwnerProcessId: null);
        return new DaemonInvalidSessionEvidence(contract);
    }
}
