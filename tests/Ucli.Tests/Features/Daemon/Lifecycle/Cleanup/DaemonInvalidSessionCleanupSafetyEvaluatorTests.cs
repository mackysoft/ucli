using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Diagnostics;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project;

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
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
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

        public DaemonProcessIdentityAssessment AssessProcess (
            Process process,
            int processId,
            DateTimeOffset expectedIssuedAtUtc)
        {
            throw new NotSupportedException();
        }
    }
}