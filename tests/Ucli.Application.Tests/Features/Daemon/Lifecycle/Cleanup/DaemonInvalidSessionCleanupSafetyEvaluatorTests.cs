using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
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

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(null);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenFingerprintDoesNotMatchAndClaimedProcessIsLive_ReturnsTrueAfterIdentityAssessment ()
    {
        var evidence = CreateEvidence(ProjectFingerprintTestFactory.Create("fingerprint-other"));
        var assessor = RecordingDaemonProcessIdentityAssessor.MatchingLiveProcess(DateTimeOffset.UtcNow);
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(evidence);

        Assert.True(requiresUnsafeSkip);
        Assert.Single(assessor.Invocations);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RequiresUnsafeSkip_WhenProcessIdIsNotPositive_ReturnsFalse (int? processId)
    {
        var evidence = CreateEvidence(ProjectFingerprintTestFactory.Create("fingerprint-current"), processId);
        var assessor = new RecordingDaemonProcessIdentityAssessor();
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(evidence);

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
        var evidence = CreateEvidence(ProjectFingerprintTestFactory.Create("fingerprint-current"));
        var assessor = new RecordingDaemonProcessIdentityAssessor(status);
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(evidence);

        Assert.Equal(expected, requiresUnsafeSkip);
        var invocation = Assert.Single(assessor.Invocations);
        Assert.Equal(1234, invocation.ProcessId);
        Assert.Equal(ProcessStartedAtUtc, invocation.ExpectedProcessStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Evidence_ExposesNoPersistedAuthorizationFields ()
    {
        Assert.Null(typeof(DaemonInvalidSessionEvidence).GetProperty("SessionToken"));
        Assert.Null(typeof(DaemonInvalidSessionEvidence).GetProperty("ProjectFingerprint"));
        Assert.Null(typeof(DaemonInvalidSessionEvidence).GetProperty("SchemaVersion"));
        Assert.Null(typeof(DaemonInvalidSessionEvidence).GetProperty("EditorMode"));
        Assert.Null(typeof(DaemonInvalidSessionEvidence).GetProperty("OwnerKind"));
        Assert.Null(typeof(DaemonInvalidSessionEvidence).GetProperty("CanShutdownProcess"));
        Assert.Null(typeof(DaemonInvalidSessionEvidence).GetProperty("OwnerProcessId"));
    }

    private static DaemonInvalidSessionEvidence CreateEvidence (
        ProjectFingerprint projectFingerprint,
        int? processId = 1234)
    {
        var contract = new DaemonSessionJsonContract(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionGenerationId: Guid.Empty,
            SessionToken: "raw-secret-must-not-be-projected",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: default,
            EditorMode: null,
            OwnerKind: null,
            CanShutdownProcess: false,
            EndpointTransportKind: null,
            EndpointAddress: null,
            ProcessId: processId,
            ProcessStartedAtUtc: ProcessStartedAtUtc,
            OwnerProcessId: null,
            EditorInstanceId: null);
        return new DaemonInvalidSessionEvidence(contract);
    }
}
