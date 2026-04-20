using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Features.Testing.Run.Execution;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

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
            failFast: true,
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
        Assert.True(payload.FailFast);
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
            failFast: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.AbnormalExit, result.FailureKind);
        Assert.Contains("payload", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcRequestTimesOut_ReturnsIpcTimedOutFailure ()
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
            failFast: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.IpcTimedOut, result.FailureKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionTokenResolutionConsumesBudget_PropagatesRemainingTimeoutToTransport ()
    {
        var timeProvider = new ManualTimeProvider();
        var daemonTransportClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new IpcTestRunResponse(0)));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("session-token"))
        {
            OnResolve = () => timeProvider.Advance(TimeSpan.FromMilliseconds(300)),
        };
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "remaining-timeout");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        scope.WriteFile("run/results.xml", "<test-run />");
        scope.WriteFile("run/editor.log", "log");
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider, timeProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            failFast: false,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(4200), daemonTransportClient.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionTokenResolutionConsumesEntireBudget_ReturnsIpcTimedOutFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var daemonTransportClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new IpcTestRunResponse(0)));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("session-token"))
        {
            OnResolve = () => timeProvider.Advance(TimeSpan.FromMilliseconds(4500)),
        };
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "session-timeout");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider, timeProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            failFast: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.IpcTimedOut, result.FailureKind);
        Assert.Equal(0, daemonTransportClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonReturnsLifecycleError_PreservesErrorCode ()
    {
        var daemonTransportClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusError,
                [
                    new IpcError(IpcErrorCodes.EditorBusy, "Unity editor is busy with internal work.", null),
                ],
                new { }));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("session-token"));
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "lifecycle-error");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            failFast: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.AbnormalExit, result.FailureKind);
        Assert.Equal(IpcErrorCodes.EditorBusy, result.ErrorCode);
        Assert.Contains(IpcErrorCodes.EditorBusy, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionTokenIsNotAvailable_PreservesDaemonNotRunningCode ()
    {
        var daemonTransportClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new IpcTestRunResponse(0)));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.SessionNotAvailable());
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "session-not-available");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            failFast: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.StartFailed, result.FailureKind);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        Assert.Equal(0, daemonTransportClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionTokenResolutionFailsInternally_ReturnsClientSetupFailure ()
    {
        var daemonTransportClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new IpcTestRunResponse(0)));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Failure(ExecutionError.InternalError("session store read failed")));
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "session-resolution-internal-error");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            failFast: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ClientSetupFailed, result.FailureKind);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal(0, daemonTransportClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenArtifactsAreMissingAfterSuccessResponse_ReturnsArtifactMissingFailure ()
    {
        var daemonTransportClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new IpcTestRunResponse(0)));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(
            DaemonSessionTokenResolutionResult.Success("session-token"));
        using var scope = TestDirectories.CreateTempScope("ipc-daemon-test-run-client", "missing-artifacts");
        var configuration = CreateConfiguration(scope);
        var artifactPaths = new ArtifactPaths(scope.GetPath("run"));
        var client = new IpcDaemonTestRunClient(daemonTransportClient, sessionTokenProvider);

        var result = await client.Execute(
            configuration,
            artifactPaths,
            TimeSpan.FromMilliseconds(4500),
            failFast: false,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityTestExecutionFailureKind.ArtifactMissing, result.FailureKind);
        Assert.Contains("results.xml", result.ErrorMessage, StringComparison.Ordinal);
    }

    private static ResolvedTestRunConfiguration CreateConfiguration (TestDirectoryScope scope)
    {
        return new ResolvedTestRunConfiguration(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: scope.GetPath("UnityProject"),
                RepositoryRoot: scope.FullPath,
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Mode: UnityExecutionMode.Daemon,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.EditMode,
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

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<IpcResponse> SendAsync (
            string storageRoot,
            string projectFingerprint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            LastTimeout = timeout;
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

        public Action? OnResolve { get; init; }

        public ValueTask<DaemonSessionTokenResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            OnResolve?.Invoke();
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