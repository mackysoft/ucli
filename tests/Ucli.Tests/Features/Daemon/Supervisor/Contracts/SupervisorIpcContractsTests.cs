using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorIpcContractsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_UsesCanonicalLiteralsWithoutRedundantDaemonStatus ()
    {
        var request = new SupervisorIpcContracts.EnsureRunningRequest(
            UnityProjectRoot: "/repository/UnityProject",
            ProjectFingerprint: SupervisorClientTestSupport.CreateUnityProject().ProjectFingerprint,
            EditorMode: DaemonEditorMode.Gui,
            OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate);
        var ensureResponse = new SupervisorIpcContracts.EnsureRunningResponse(
            StartStatus: DaemonStartStatus.Attached,
            Session: DaemonSessionContractMapper.ToContract(SupervisorClientTestSupport.CreateGuiDaemonSession()),
            LifecycleObservation: null);
        var stopResponse = new SupervisorIpcContracts.StopProjectResponse(DaemonStopStatus.Stopped);

        var requestJson = IpcPayloadCodec.SerializeToElement(request);
        var ensureResponseJson = IpcPayloadCodec.SerializeToElement(ensureResponse);
        var stopResponseJson = IpcPayloadCodec.SerializeToElement(stopResponse);

        Assert.Equal("gui", requestJson.GetProperty("editorMode").GetString());
        Assert.Equal("terminate", requestJson.GetProperty("onStartupBlocked").GetString());
        Assert.Equal("attached", ensureResponseJson.GetProperty("startStatus").GetString());
        Assert.False(ensureResponseJson.TryGetProperty("daemonStatus", out _));
        Assert.Equal("stopped", stopResponseJson.GetProperty("stopStatus").GetString());
        Assert.False(stopResponseJson.TryGetProperty("daemonStatus", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDeserialize_WhenRequiredStartupPolicyIsMissing_ReturnsFalse ()
    {
        var rawPayload = JsonSerializer.SerializeToElement(
            new
            {
                UnityProjectRoot = "/repository/UnityProject",
                ProjectFingerprint = SupervisorClientTestSupport.CreateUnityProject().ProjectFingerprint,
                EditorMode = (string?)null,
            },
            IpcJsonSerializerOptions.Default);

        var succeeded = IpcPayloadCodec.TryDeserialize(
            rawPayload,
            out SupervisorIpcContracts.EnsureRunningRequest _,
            out _);

        Assert.False(succeeded);
    }
}
