using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcDaemonContractSerializationTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly ProjectFingerprint ProjectFingerprint = new(ProjectFingerprintText);
    private static readonly Guid LaunchAttemptId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonStartSupervisorProgressContracts_SerializeWithCamelCaseFields ()
    {
        AssertPayloadKind(DaemonStartProgressEvent.Launching, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.WaitingForEndpoint, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.BlockerDetected, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.SessionRegistered, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.EndpointRegistered, DaemonStartProgressPayloadKind.StartupObservation);
        AssertPayloadKind(DaemonStartProgressEvent.LifecycleObserved, DaemonStartProgressPayloadKind.LifecycleSnapshot);
        Assert.False(DaemonStartProgressPayloadContract.TryGetPayloadKind(
            DaemonStartProgressEvent.Completed,
            out _));

        var startupObservation = IpcPayloadCodec.SerializeToElement(
            new DaemonStartStartupObservationProgressEntry(
                DaemonStartProgressPayloadKind.StartupObservation,
                ProjectFingerprint,
                120000,
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Terminate,
                LaunchAttemptId,
                DaemonSessionOwnerKind.Cli,
                true,
                1234,
                DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
                DaemonStartupStatus.Blocked,
                DaemonStartupBlockingReason.Compile,
                DaemonDiagnosisStartupPhase.ScriptCompilation,
                DaemonStartupRetryDisposition.RetryAfterFix,
                "Unity scripts failed to compile.",
                "UNITY_SCRIPT_COMPILATION_FAILED"));
        var lifecycleSnapshot = IpcPayloadCodec.SerializeToElement(
            new DaemonStartLifecycleSnapshotProgressEntry(
                DaemonStartProgressPayloadKind.LifecycleSnapshot,
                ProjectFingerprint,
                120000,
                DaemonEditorMode.Batchmode,
                DaemonStartupBlockedProcessPolicy.Terminate,
                IpcEditorLifecycleState.Ready,
                null,
                new IpcUnityGenerationSnapshot(1, 2, 3, 4),
                true));

        JsonAssert.For(startupObservation)
            .HasString("payloadKind", "startupObservation")
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasInt32("timeoutMilliseconds", 120000)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "terminate")
            .HasString("launchAttemptId", LaunchAttemptId.ToString("D"))
            .HasString("ownerKind", "cli")
            .HasBoolean("canShutdownProcess", true)
            .HasInt32("processId", 1234)
            .HasString("processStartedAtUtc", "2026-05-21T00:00:00+00:00")
            .HasString("startupStatus", "blocked")
            .HasString("startupBlockingReason", "compile")
            .HasString("startupPhase", "scriptCompilation")
            .HasString("retryDisposition", "retryAfterFix")
            .HasString("message", "Unity scripts failed to compile.")
            .HasString("errorCode", "UNITY_SCRIPT_COMPILATION_FAILED");
        Assert.False(startupObservation.TryGetProperty("lifecycleState", out _));
        Assert.False(startupObservation.TryGetProperty("blockingReason", out _));
        Assert.False(startupObservation.TryGetProperty("canAcceptExecutionRequests", out _));

        JsonAssert.For(lifecycleSnapshot)
            .HasString("payloadKind", "lifecycleSnapshot")
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasInt32("timeoutMilliseconds", 120000)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "terminate")
            .HasString("lifecycleState", "ready")
            .HasValueKind("blockingReason", JsonValueKind.Null)
            .HasBoolean("canAcceptExecutionRequests", true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonStartStartupObservationProgressEntry_WithEmptyLaunchAttemptId_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => new DaemonStartStartupObservationProgressEntry(
            DaemonStartProgressPayloadKind.StartupObservation,
            ProjectFingerprint,
            120000,
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Terminate,
            Guid.Empty,
            DaemonSessionOwnerKind.Cli,
            true,
            1234,
            DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
            DaemonStartupStatus.Blocked,
            DaemonStartupBlockingReason.Compile,
            DaemonDiagnosisStartupPhase.ScriptCompilation,
            DaemonStartupRetryDisposition.RetryAfterFix,
            "Unity scripts failed to compile.",
            "UNITY_SCRIPT_COMPILATION_FAILED"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonStartStartupObservationProgressEntry_WithMismatchedPayloadKind_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DaemonStartStartupObservationProgressEntry(
            DaemonStartProgressPayloadKind.LifecycleSnapshot,
            ProjectFingerprint,
            120000,
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Terminate,
            LaunchAttemptId,
            DaemonSessionOwnerKind.Cli,
            true,
            1234,
            DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
            DaemonStartupStatus.Blocked,
            DaemonStartupBlockingReason.Compile,
            DaemonDiagnosisStartupPhase.ScriptCompilation,
            DaemonStartupRetryDisposition.RetryAfterFix,
            "Unity scripts failed to compile.",
            "UNITY_SCRIPT_COMPILATION_FAILED"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonStartLifecycleSnapshotProgressEntry_WithInconsistentLifecycleTuple_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => new DaemonStartLifecycleSnapshotProgressEntry(
            DaemonStartProgressPayloadKind.LifecycleSnapshot,
            ProjectFingerprint,
            120000,
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Terminate,
            IpcEditorLifecycleState.Compiling,
            IpcEditorBlockingReason.Busy,
            new IpcUnityGenerationSnapshot(1, 2, 3, 4),
            false));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcShutdownContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcShutdownRequest(RequestedBy: "ucli-daemon-stop");
        var responsePayload = new IpcShutdownResponse(Accepted: true, Message: "Shutdown accepted.");

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasString("requestedBy", "ucli-daemon-stop");
        JsonAssert.For(response)
            .HasBoolean("accepted", true)
            .HasString("message", "Shutdown accepted.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonStartProgressContracts_SerializeWithCamelCaseFields ()
    {
        var payload = new DaemonStartProgressEntry(
            ProjectFingerprint: ProjectFingerprint,
            TimeoutMilliseconds: 10000,
            EditorMode: DaemonEditorMode.Batchmode,
            OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            Result: CommandProgressResult.Failed,
            StartStatus: "failed",
            DaemonStatus: "notRunning",
            ErrorCode: "IPC_TIMEOUT");

        var document = IpcPayloadCodec.SerializeToElement(payload);

        JsonAssert.For(document)
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasInt32("timeoutMilliseconds", 10000)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "auto")
            .HasString("result", "failed")
            .HasString("startStatus", "failed")
            .HasString("daemonStatus", "notRunning")
            .HasString("errorCode", "IPC_TIMEOUT");
    }

    private static void AssertPayloadKind (
        DaemonStartProgressEvent progressEvent,
        DaemonStartProgressPayloadKind expectedPayloadKind)
    {
        Assert.True(DaemonStartProgressPayloadContract.TryGetPayloadKind(progressEvent, out var payloadKind));
        Assert.Equal(expectedPayloadKind, payloadKind);
    }
}
