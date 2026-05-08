using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonInvalidSessionCleanupSafetyEvaluatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenSessionSnapshotIsNull_ReturnsFalse ()
    {
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(new StubDaemonProcessIdentityAssessor());

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(CreateContext("fingerprint-invalid-null"), null);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenFingerprintMismatchAndProcessIdIsMissing_ReturnsFalse ()
    {
        var context = CreateContext("fingerprint-invalid-mismatch-no-pid");
        var session = CreateSession("different-fingerprint") with
        {
            ProcessId = null,
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(new StubDaemonProcessIdentityAssessor());

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenFingerprintMismatchAndIdentityAssessmentIsNotRunning_ReturnsFalse ()
    {
        var context = CreateContext("fingerprint-invalid-mismatch-not-running");
        var session = CreateSession("different-fingerprint");
        var assessor = new StubDaemonProcessIdentityAssessor
        {
            NextAssessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.NotRunning,
                null,
                null),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
        Assert.Null(assessor.LastProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenFingerprintMismatchAndIdentityAssessmentIsMatchingLiveProcess_ReturnsFalse ()
    {
        var context = CreateContext("fingerprint-invalid-mismatch-live");
        var session = CreateSession("different-fingerprint");
        var assessor = new StubDaemonProcessIdentityAssessor
        {
            NextAssessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess,
                DateTimeOffset.UtcNow,
                null),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
        Assert.Null(assessor.LastProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenProcessIdIsMissing_ReturnsFalse ()
    {
        var context = CreateContext("fingerprint-invalid-no-pid");
        var session = CreateSession(context.ProjectFingerprint) with
        {
            ProcessId = null,
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(new StubDaemonProcessIdentityAssessor());

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenIssuedAtUtcIsDefault_ReturnsFalse ()
    {
        var context = CreateContext("fingerprint-invalid-no-issued-at");
        var session = CreateSession(context.ProjectFingerprint) with
        {
            IssuedAtUtc = default,
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(new StubDaemonProcessIdentityAssessor());

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenIdentityAssessmentIsNotRunning_ReturnsFalse ()
    {
        var context = CreateContext("fingerprint-invalid-not-running");
        var session = CreateSession(context.ProjectFingerprint);
        var assessor = new StubDaemonProcessIdentityAssessor
        {
            NextAssessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.NotRunning,
                null,
                null),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.False(requiresUnsafeSkip);
        Assert.Equal(session.ProcessId, assessor.LastProcessId);
        Assert.Equal(session.IssuedAtUtc, assessor.LastIssuedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiresUnsafeSkip_WhenIdentityAssessmentIsDifferentProcess_ReturnsFalse ()
    {
        var context = CreateContext("fingerprint-invalid-different-process");
        var session = CreateSession(context.ProjectFingerprint);
        var assessor = new StubDaemonProcessIdentityAssessor
        {
            NextAssessment = new DaemonProcessIdentityAssessment(
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
        var context = CreateContext("fingerprint-invalid-live");
        var session = CreateSession(context.ProjectFingerprint);
        var assessor = new StubDaemonProcessIdentityAssessor
        {
            NextAssessment = new DaemonProcessIdentityAssessment(
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
        var context = CreateContext("fingerprint-invalid-uncertain");
        var session = CreateSession(context.ProjectFingerprint);
        var assessor = new StubDaemonProcessIdentityAssessor
        {
            NextAssessment = new DaemonProcessIdentityAssessment(
                DaemonProcessIdentityAssessmentStatus.Uncertain,
                null,
                ExecutionError.InternalError("probe failed")),
        };
        var evaluator = new DaemonInvalidSessionCleanupSafetyEvaluator(assessor);

        var requiresUnsafeSkip = evaluator.RequiresUnsafeSkip(context, session);

        Assert.True(requiresUnsafeSkip);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (string projectFingerprint)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 2468,
            OwnerProcessId: 1357);
    }

    private sealed class StubDaemonProcessIdentityAssessor : IDaemonProcessIdentityAssessor
    {
        public DaemonProcessIdentityAssessment NextAssessment { get; set; } = new(
            DaemonProcessIdentityAssessmentStatus.NotRunning,
            null,
            null);

        public int? LastProcessId { get; private set; }

        public DateTimeOffset LastIssuedAtUtc { get; private set; }

        public DaemonProcessIdentityAssessment AssessByProcessId (
            int processId,
            DateTimeOffset expectedIssuedAtUtc)
        {
            LastProcessId = processId;
            LastIssuedAtUtc = expectedIssuedAtUtc;
            return NextAssessment;
        }

    }
}
