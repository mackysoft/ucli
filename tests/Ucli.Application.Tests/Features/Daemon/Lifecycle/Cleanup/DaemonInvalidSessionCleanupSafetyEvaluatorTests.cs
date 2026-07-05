using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonInvalidSessionCleanupSafetyEvaluatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenSessionSnapshotIsNull_ReturnsFalse ()
    {
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor());

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-null"), null);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenFingerprintMismatchAndProcessIdIsMissing_ReturnsFalse ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-mismatch-no-pid");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: "different-fingerprint") with
        {
            ProcessId = null,
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor());

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenFingerprintMismatch_ReturnsFalseBeforeIdentityAssessment ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-mismatch-not-running");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: "different-fingerprint");
        var assessor = new UnexpectedDaemonProcessIdentityAssessor("Fingerprint mismatch should stop before process identity assessment.");
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenProcessIdIsMissing_ReturnsFalse ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-no-pid");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: context.ProjectFingerprint) with
        {
            ProcessId = null,
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(new RecordingDaemonProcessIdentityAssessor());

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenIssuedAtUtcIsDefault_StillUsesProcessStartedAtUtcForIdentity ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-no-issued-at");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: context.ProjectFingerprint) with
        {
            IssuedAtUtc = default,
        };
        var assessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                DateTimeOffset.UtcNow,
                null),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.True(requiresUnsafeSkip);
        DaemonProcessIdentityAssessorAssert.AssessedOnceForSession(assessor, session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenIdentityAssessmentIsNotRunning_ReturnsFalse ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-not-running");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: context.ProjectFingerprint);
        var assessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.NotRunning,
                null,
                null),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
        DaemonProcessIdentityAssessorAssert.AssessedOnceForSession(assessor, session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenIdentityAssessmentIsDifferentProcess_ReturnsFalse ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-different-process");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: context.ProjectFingerprint);
        var assessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.DifferentProcess,
                DateTimeOffset.UtcNow,
                ExecutionError.InternalError("identity mismatch")),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenIdentityAssessmentIsMatchingLiveProcess_ReturnsTrue ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-live");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: context.ProjectFingerprint);
        var assessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                DateTimeOffset.UtcNow,
                null),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.True(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenIdentityAssessmentIsUncertain_ReturnsTrue ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-invalid-uncertain");
        var session = DaemonSessionTestFactory.Create(projectFingerprint: context.ProjectFingerprint);
        var assessor = new RecordingDaemonProcessIdentityAssessor
        {
            Assessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.Uncertain,
                null,
                ExecutionError.InternalError("probe failed")),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.True(requiresUnsafeSkip);
    }
}
