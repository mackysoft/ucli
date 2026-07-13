using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherEnsureRunningTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEditorModeIsSpecified_PassesNormalizedValueToStartOperation ()
    {
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(IpcEditorLifecycleState.Compiling);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(
                DaemonSessionTestFactory.Create(
                    sessionToken: "session-token",
                    issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
                    endpointTransportKind: "unixDomainSocket",
                    endpointAddress: "/tmp/ucli.sock",
                    processId: 42,
                    ownerProcessId: 24),
                lifecycleSnapshot),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-editor-mode",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: " gui ",
                        OnStartupBlocked: " terminate ")),
                responseMode: IpcResponseMode.Single));

        Assert.True(
            string.Equals(IpcProtocol.StatusOk, response.Status, StringComparison.Ordinal),
            string.Join(Environment.NewLine, response.Errors.Select(error => $"{error.Code.Value}: {error.Message}")));
        DaemonStartOperationAssert.EnsureRunningRequested(
            startOperation,
            runtimeContext.StorageRoot,
            unityProjectRoot,
            projectFingerprint,
            TimeSpan.FromMilliseconds(1000),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse payload,
            out _));
        Assert.Equal(lifecycleSnapshot, payload.LifecycleSnapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenStartOperationAttaches_EmitsAttachedStartStatus ()
    {
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            canShutdownProcess: false,
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ucli.sock",
            processId: 42,
            ownerProcessId: 24);
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(IpcEditorLifecycleState.Ready);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.Attached(session, lifecycleSnapshot),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-attached",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: "gui",
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse payload,
            out _));
        Assert.Equal("attached", payload.StartStatus);
        Assert.Equal(session, payload.Session);
        Assert.Equal(lifecycleSnapshot, payload.LifecycleSnapshot);
    }
}
