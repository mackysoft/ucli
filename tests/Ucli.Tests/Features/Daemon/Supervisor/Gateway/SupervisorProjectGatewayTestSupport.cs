using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorProjectGatewayTestSupport
{
    public const string ProjectFingerprint = "fingerprint";

    public const int StartTimeoutMilliseconds = 900;

    public static async ValueTask<SupervisorProjectGatewayScenario> CreateManifestBackedScenarioAsync (
        string repositoryRoot,
        TimeProvider? timeProvider = null)
    {
        var effectiveTimeProvider = timeProvider ?? new ManualTimeProvider();
        var manifest = SupervisorClientTestSupport.CreateManifest();
        var manifestStore = SupervisorManifestStoreTestSupport.CreateFileBacked(
            effectiveTimeProvider);
        await manifestStore.WriteAsync(repositoryRoot, manifest, CancellationToken.None).ConfigureAwait(false);

        var transportClient = new StubIpcTransportClient();
        var client = new SupervisorClient(transportClient, effectiveTimeProvider);
        var launcher = new RecordingSupervisorProcessLauncher();
        var gateway = CreateGateway(
            manifestStore,
            client,
            launcher,
            effectiveTimeProvider);

        return new SupervisorProjectGatewayScenario(
            repositoryRoot,
            manifest,
            manifestStore,
            transportClient,
            gateway);
    }

    public static SupervisorProjectGateway CreateGateway (
        SupervisorManifestStore manifestStore,
        SupervisorClient client,
        RecordingSupervisorProcessLauncher launcher,
        TimeProvider? timeProvider = null)
    {
        var effectiveTimeProvider = timeProvider ?? new ManualTimeProvider();
        return new SupervisorProjectGateway(
            new SupervisorBootstrapper(
                manifestStore,
                client,
                launcher,
                new SupervisorBootstrapLockProvider(effectiveTimeProvider),
                new SupervisorEndpointResolver(),
                effectiveTimeProvider),
            manifestStore,
            client,
            new SupervisorBootstrapLockProvider(effectiveTimeProvider),
            new SupervisorEndpointResolver(),
            effectiveTimeProvider);
    }

    public static ResolvedUnityProjectContext CreateUnityProject (
        string repositoryRoot,
        string projectFingerprint = ProjectFingerprint)
    {
        return ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
            repositoryRoot,
            projectFingerprint: projectFingerprint);
    }

    public static DaemonStartProgressEmitter CreateStartProgressEmitter (
        CollectingCommandProgressSink progressSink,
        int timeoutMilliseconds = StartTimeoutMilliseconds,
        DaemonEditorMode? editorMode = null,
        DaemonStartupBlockedProcessPolicy onStartupBlocked = DaemonStartupBlockedProcessPolicy.Auto)
    {
        return new DaemonStartProgressEmitter(
            progressSink,
            ProjectFingerprint,
            timeoutMilliseconds,
            editorMode,
            onStartupBlocked);
    }

    public static IpcResponse CreateSupervisorPingResponse (
        IpcRequest request,
        SupervisorInstanceManifest manifest)
    {
        return IpcResponseTestFactory.CreateSuccess(
            request,
            new SupervisorIpcContracts.PingResponse(
                manifest.ProcessId,
                manifest.IssuedAtUtc));
    }

    public static IpcResponse CreateStartedEnsureRunningResponse (IpcRequest request)
    {
        return SupervisorClientTestSupport.CreateEnsureRunningResponse(
            request,
            startStatus: "started",
            daemonStatus: "running");
    }

    public static IpcResponse CreateStopProjectStoppedResponse (IpcRequest request)
    {
        return IpcResponseTestFactory.CreateSuccess(
            request,
            new SupervisorIpcContracts.StopProjectResponse(
                StopStatus: "stopped",
                DaemonStatus: "notRunning"));
    }

    public static SupervisorIpcContracts.EnsureRunningRequest ReadEnsureRunningRequest (IpcRequest request)
    {
        Assert.True(IpcPayloadCodec.TryDeserialize(
            request.Payload,
            out SupervisorIpcContracts.EnsureRunningRequest payload,
            out _));
        return payload;
    }

    public static SupervisorIpcContracts.StopProjectRequest ReadStopProjectRequest (IpcRequest request)
    {
        Assert.True(IpcPayloadCodec.TryDeserialize(
            request.Payload,
            out SupervisorIpcContracts.StopProjectRequest payload,
            out _));
        return payload;
    }
}

internal sealed class SupervisorProjectGatewayScenario
{
    private readonly string repositoryRoot;

    public SupervisorProjectGatewayScenario (
        string repositoryRoot,
        SupervisorInstanceManifest manifest,
        SupervisorManifestStore manifestStore,
        StubIpcTransportClient transportClient,
        SupervisorProjectGateway gateway)
    {
        this.repositoryRoot = repositoryRoot;
        Manifest = manifest;
        ManifestStore = manifestStore;
        TransportClient = transportClient;
        Gateway = gateway;
    }

    public SupervisorInstanceManifest Manifest { get; }

    public SupervisorManifestStore ManifestStore { get; }

    public StubIpcTransportClient TransportClient { get; }

    public SupervisorProjectGateway Gateway { get; }

    public ResolvedUnityProjectContext CreateUnityProject (
        string projectFingerprint = SupervisorProjectGatewayTestSupport.ProjectFingerprint)
    {
        return SupervisorProjectGatewayTestSupport.CreateUnityProject(
            repositoryRoot,
            projectFingerprint);
    }
}
