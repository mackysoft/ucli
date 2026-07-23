using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Daemon;

internal static class DaemonLaunchAttemptStoreTestSupport
{
    internal static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");

    internal static Guid CreateLaunchAttemptId (int sequence)
    {
        return new Guid(sequence, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    internal static DaemonLaunchAttempt CreateAttempt (
        Guid launchAttemptId,
        string storageRoot,
        DaemonStartupStatus startupStatus,
        int minuteOffset = 0)
    {
        var updatedAtUtc = new DateTimeOffset(2026, 03, 12, 0, minuteOffset, 0, TimeSpan.Zero);
        var guardedStorageRoot = AbsolutePath.Parse(storageRoot);
        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(guardedStorageRoot, ProjectFingerprint);
        var diagnosis = new DaemonDiagnosis(
            Reason: DaemonDiagnosisReason.StartupFailed,
            Message: "startup failed",
            ReportedBy: DaemonDiagnosisReportedBy.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: 1234,
            EditorInstancePath: null,
            SessionIssuedAtUtc: updatedAtUtc,
            ProcessStartedAtUtc: updatedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
            ActionRequired: DaemonDiagnosisActionRequired.InspectUnityLog,
            PrimaryDiagnostic: new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                Code: "CS0103",
                File: "Assets/Foo.cs",
                Line: 12,
                Column: 34,
                Message: "Missing type"));
        return new DaemonLaunchAttempt(
            LaunchAttemptId: launchAttemptId,
            StartedAtUtc: updatedAtUtc,
            UpdatedAtUtc: updatedAtUtc,
            StartupStatus: startupStatus,
            StartupBlockingReason: DaemonStartupBlockingReason.Unknown,
            RetryDisposition: DaemonStartupRetryDisposition.Unknown,
            ProcessAction: DaemonStartupProcessAction.None,
            EditorMode: DaemonEditorMode.Gui,
            ProcessId: 1234,
            ProcessStartedAtUtc: updatedAtUtc,
            UnityLogPath: unityLogPath,
            ArtifactPath: UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
                guardedStorageRoot,
                ProjectFingerprint,
                launchAttemptId),
            Diagnosis: diagnosis);
    }

    internal static async Task WriteAttemptAsync (
        DaemonLaunchAttemptStore store,
        string storageRoot,
        DaemonLaunchAttempt attempt)
    {
        var writeResult = await store.WriteFailureAsync(
            AbsolutePath.Parse(storageRoot),
            ProjectFingerprint,
            attempt,
            CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
    }
}
