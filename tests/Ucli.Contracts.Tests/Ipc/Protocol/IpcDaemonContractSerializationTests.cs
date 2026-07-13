using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcDaemonContractSerializationTests
{
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
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                "project-fingerprint",
                120000,
                ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                ContractLiteralCodec.ToValue(DaemonStartupBlockedProcessPolicy.Terminate),
                "attempt-1",
                ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
                true,
                1234,
                DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
                ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
                ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile),
                ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.ScriptCompilation),
                ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix),
                "Unity scripts failed to compile.",
                "UNITY_SCRIPT_COMPILATION_FAILED"));
        var lifecycleSnapshot = IpcPayloadCodec.SerializeToElement(
            new DaemonStartLifecycleSnapshotProgressEntry(
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.LifecycleSnapshot),
                "project-fingerprint",
                120000,
                ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                ContractLiteralCodec.ToValue(DaemonStartupBlockedProcessPolicy.Terminate),
                ContractLiteralCodec.ToValue(IpcEditorLifecycleState.Ready),
                null,
                true));

        JsonAssert.For(startupObservation)
            .HasString("payloadKind", "startupObservation")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasInt32("timeoutMilliseconds", 120000)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "terminate")
            .HasString("launchAttemptId", "attempt-1")
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
            .HasString("projectFingerprint", "project-fingerprint")
            .HasInt32("timeoutMilliseconds", 120000)
            .HasString("editorMode", "batchmode")
            .HasString("onStartupBlocked", "terminate")
            .HasString("lifecycleState", "ready")
            .HasValueKind("blockingReason", JsonValueKind.Null)
            .HasBoolean("canAcceptExecutionRequests", true);
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
            ProjectFingerprint: "project-fingerprint",
            TimeoutMilliseconds: 10000,
            EditorMode: "batchmode",
            OnStartupBlocked: "auto",
            Result: ContractLiteralCodec.ToValue(CommandProgressResult.Failed),
            StartStatus: "failed",
            DaemonStatus: "notRunning",
            ErrorCode: "IPC_TIMEOUT");

        var document = IpcPayloadCodec.SerializeToElement(payload);

        JsonAssert.For(document)
            .HasString("projectFingerprint", "project-fingerprint")
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
