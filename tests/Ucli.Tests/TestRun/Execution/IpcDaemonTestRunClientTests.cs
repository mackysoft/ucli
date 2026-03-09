using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

public sealed class IpcDaemonTestRunClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithValidResponse_ReturnsSuccess ()
    {
        var daemonTransportClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new IpcTestRunResponse(0)));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("session-token"));
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "success");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        scope.WriteFile("run/results.xml", "<test-run />");
        scope.WriteFile("run/editor.log", "log");
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ProcessExitCode);
        Assert.Equal(1, daemonTransportClient.CallCount);
        var request = Assert.IsType<IpcRequest>(daemonTransportClient.LastRequest);
        Assert.Equal(IpcMethodNames.TestRun, request.Method);
        Assert.Equal("session-token", request.SessionToken);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcTestRunRequest payload, out _));
        Assert.Equal("editmode", payload.TestPlatform);
        Assert.Equal(artifactPaths.ResultsXmlPath, payload.ResultsXmlPath);
        Assert.Equal(artifactPaths.EditorLogPath, payload.EditorLogPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenResponsePayloadIsInvalid_ReturnsAbnormalExitFailure ()
    {
        var daemonTransportClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new
                {
                    unknown = true,
                }));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("session-token"));
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "invalid-payload");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.AbnormalExit, result.FailureKind);
        Assert.Contains("payload", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcRequestTimesOut_ReturnsTimedOutFailure ()
    {
        var daemonTransportClient = new StubUnityIpcTransportClient((_) => throw new TimeoutException("timeout"));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("session-token"));
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "timeout");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.TimedOut, result.FailureKind);
    }

    private static ResolvedTestRunConfiguration CreateConfiguration (TestDirectoryScope scope)
    {
        return new ResolvedTestRunConfiguration(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: scope.GetPath("UnityProject"),
                RepositoryRoot: scope.FullPath,
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Mode: "daemon",
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: IpcTestRunPlatform.EditMode,
            RawTestPlatform: "editmode",
            BuildTarget: null,
            TestFilter: null,
            TestCategories: [],
            AssemblyNames: [],
            TestSettingsPath: null,
            TimeoutMilliseconds: null);
    }

    private sealed class StubUnityIpcTransportClient : IUnityIpcTransportClient
    {
        private readonly Func<IpcRequest, IpcResponse> responseFactory;

        public StubUnityIpcTransportClient (Func<IpcRequest, IpcResponse> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public int CallCount { get; private set; }

        public IpcRequest? LastRequest { get; private set; }

        public ValueTask<IpcResponse> SendAsync (
            string storageRoot,
            string projectFingerprint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return ValueTask.FromResult(responseFactory(request));
        }
    }

    private sealed class StubDaemonSessionTokenProvider : IDaemonSessionTokenProvider
    {
        private readonly DaemonSessionTokenResolutionResult result;

        public StubDaemonSessionTokenProvider (DaemonSessionTokenResolutionResult result)
        {
            this.result = result;
        }

        public ValueTask<DaemonSessionTokenResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private static IpcResponse CreateResponse (
        IpcRequest request,
        string status,
        IReadOnlyList<IpcError> errors,
        object payload)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors);
    }
}
