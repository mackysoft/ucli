using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
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

    [Fact]
    [Trait("Size", "Small")]
    public void FailurePayloadMapper_WhenAnyFilesystemPathIsRelative_RejectsTransportMetadata ()
    {
        var editorInstancePath = AbsolutePath.Parse(Path.Combine(
            Path.GetTempPath(),
            "ucli-supervisor-contracts",
            "EditorInstance.json"));
        var unityLogPath = AbsolutePath.Parse(Path.Combine(
            Path.GetTempPath(),
            "ucli-supervisor-contracts",
            "Editor.log"));
        var artifactPath = AbsolutePath.Parse(Path.Combine(
            Path.GetTempPath(),
            "ucli-supervisor-contracts",
            "launch-attempt.json"));
        var diagnosis = DaemonDiagnosisTestFactory.Create(
            editorInstancePath: editorInstancePath,
            unityLogPath: unityLogPath);
        var startup = new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Timeout,
            StartupBlockingReason: DaemonStartupBlockingReason.Unknown,
            LaunchAttemptId: null,
            ProcessAction: DaemonStartupProcessAction.Kept,
            RetryDisposition: DaemonStartupRetryDisposition.RetryImmediately,
            EditorMode: null,
            OwnerKind: null,
            CanShutdownProcess: null,
            ProcessId: null,
            StartedAtUtc: null,
            ElapsedMilliseconds: null,
            ArtifactPath: artifactPath);
        var validContract = SupervisorEnsureRunningFailurePayloadMapper.ToContract(
            DaemonStatusKind.Stale,
            diagnosis,
            startup);

        var relativeEditorInstancePathContract = validContract with
        {
            Diagnosis = validContract.Diagnosis! with
            {
                EditorInstancePath = Path.Combine("Library", "EditorInstance.json"),
            },
        };
        var relativeUnityLogPathContract = validContract with
        {
            Diagnosis = validContract.Diagnosis! with
            {
                UnityLogPath = "Editor.log",
            },
        };
        var relativeArtifactPathContract = validContract with
        {
            Startup = validContract.Startup! with
            {
                ArtifactPath = "launch-attempt.json",
            },
        };

        Assert.False(SupervisorEnsureRunningFailurePayloadMapper.TryToMetadata(
            relativeEditorInstancePathContract,
            out _));
        Assert.False(SupervisorEnsureRunningFailurePayloadMapper.TryToMetadata(
            relativeUnityLogPathContract,
            out _));
        Assert.False(SupervisorEnsureRunningFailurePayloadMapper.TryToMetadata(
            relativeArtifactPathContract,
            out _));
    }
}
