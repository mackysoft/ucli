using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.TestDoubles;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessful_AcquiresProjectLockAndUsesConfiguredEndpoint ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "success");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var lockProvider = new StubProjectLifecycleLockProvider();
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            lockProvider,
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var lockRequest = Assert.IsType<ProjectLifecycleLockRequest>(lockProvider.LastRequest);
        Assert.Equal(unityProject.UnityProjectRoot, lockRequest.UnityProjectRoot);
        Assert.Equal(
            UcliStoragePathResolver.ResolveUnityLogPath(unityProject.RepositoryRoot, unityProject.ProjectFingerprint),
            launcher.LastUnityLogPath);

        var bootstrapArguments = Assert.IsType<IpcOneshotBootstrapArguments>(launcher.LastBootstrapArguments);
        Assert.Equal(Environment.ProcessId, bootstrapArguments.ParentProcessId);
        Assert.False(string.IsNullOrWhiteSpace(bootstrapArguments.SessionToken));
        Assert.True(bootstrapArguments.ExitDeadlineUtc > DateTimeOffset.UtcNow);
        Assert.Equal(IpcTransportKindValues.UnixDomainSocket, bootstrapArguments.EndpointTransportKind);
        Assert.Equal(endpoint.Address, bootstrapArguments.EndpointAddress);

        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[0].Method);
        Assert.Equal(IpcMethodNames.OpsRead, transportClient.Requests[1].Method);
        Assert.Equal(CreateDispatchPayload().GetRawText(), transportClient.Requests[1].Payload.GetRawText());
        Assert.All(
            transportClient.Requests,
            request => Assert.Equal(bootstrapArguments.SessionToken, request.SessionToken));
        Assert.Equal(1, processHandle.WaitForExitCallCount);
        Assert.Equal(0, processHandle.TerminateCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenStartupPingProjectFingerprintMismatches_ReturnsFailureWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-fingerprint-mismatch");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId, projectFingerprint: "other-project-fingerprint"),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("projectFingerprint mismatch", result.Message, StringComparison.Ordinal);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[0].Method);
        Assert.Equal(IpcMethodNames.Shutdown, transportClient.Requests[1].Method);
        Assert.DoesNotContain(transportClient.Requests, static request => string.Equals(request.Method, IpcMethodNames.OpsRead, StringComparison.Ordinal));
        Assert.Equal(1, processHandle.WaitForExitCallCount);
        Assert.Equal(0, processHandle.TerminateCallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcEditorLifecycleStateCodec.Starting)]
    public async Task SendAsync_WhenStartupPingReportsWaitableState_RetriesUntilReadyBeforeSendingRequest (
        string lifecycleState)
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", $"startup-retry-{lifecycleState}");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: ++pingAttempt == 1 ? lifecycleState : IpcEditorLifecycleStateCodec.Ready,
                    canAcceptExecutionRequests: pingAttempt != 1),
                IpcMethodNames.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[0].Method);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[1].Method);
        Assert.Equal(IpcMethodNames.OpsRead, transportClient.Requests[2].Method);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcEditorLifecycleStateCodec.CompileFailed)]
    [InlineData(IpcEditorLifecycleStateCodec.SafeMode)]
    public async Task SendAsync_WhenStartupPingReportsAllowedLifecycleState_DispatchesRequestWithoutReadiness (
        string lifecycleState)
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", $"startup-allowed-{lifecycleState}");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: lifecycleState,
                    canAcceptExecutionRequests: false),
                IpcMethodNames.Compile => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateCompileDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[0].Method);
        Assert.Equal(IpcMethodNames.Compile, transportClient.Requests[1].Method);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenStartupPingReportsWaitableStateAndRequestIsFailFast_ReturnsLifecycleFailureWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-fail-fast");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(
                    request.RequestId,
                    lifecycleState: IpcEditorLifecycleStateCodec.Starting,
                    canAcceptExecutionRequests: false),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateOpsReadDispatchRequest(failFast: true, requireReadinessGate: true),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorStarting, result.ErrorCode);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[0].Method);
        Assert.Equal(IpcMethodNames.Shutdown, transportClient.Requests[1].Method);
        Assert.Equal(1, processHandle.TerminateCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenReadyPingDispatchSucceeds_SendsShutdownBeforeWaitingForExit ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "ready-ping-shutdown");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => HandlePing(request),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateReadyPingDispatchRequest(failFast: false),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[0].Method);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[1].Method);
        Assert.Equal(IpcMethodNames.Shutdown, transportClient.Requests[2].Method);
        var bootstrapArguments = Assert.IsType<IpcOneshotBootstrapArguments>(launcher.LastBootstrapArguments);
        Assert.Equal(bootstrapArguments.SessionToken, transportClient.Requests[2].SessionToken);
        Assert.Equal(1, processHandle.WaitForExitCallCount);
        Assert.Equal(0, processHandle.TerminateCallCount);

        static IpcResponse HandlePing (IpcRequest request)
        {
            Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPingRequest payload, out _));
            return payload.ClientVersion switch
            {
                IpcPingClientVersions.OneshotStartup => CreatePingResponse(request.RequestId),
                IpcPingClientVersions.Ready => CreatePingResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected ping client: {payload.ClientVersion}"),
            };
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenReadyPingRequestIsFailFast_UsesFailFastStartupProbeWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "ready-ping-startup-fail-fast");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => HandleStartupPing(request),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateReadyPingDispatchRequest(failFast: true),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorStarting, result.ErrorCode);
        Assert.Equal(2, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Ping, transportClient.Requests[0].Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(transportClient.Requests[0].Payload, out IpcPingRequest startupPing, out _));
        Assert.Equal(IpcPingClientVersions.OneshotStartup, startupPing.ClientVersion);
        Assert.Equal(IpcMethodNames.Shutdown, transportClient.Requests[1].Method);
        Assert.Equal(0, processHandle.TerminateCallCount);

        static IpcResponse HandleStartupPing (IpcRequest request)
        {
            Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcPingRequest payload, out _));
            Assert.Equal(IpcPingClientVersions.OneshotStartup, payload.ClientVersion);
            return CreatePingResponse(
                request.RequestId,
                lifecycleState: IpcEditorLifecycleStateCodec.Starting,
                canAcceptExecutionRequests: false);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenStartupPingTimesOut_RetriesUntilReachable ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-timeout-retry");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping when ++pingAttempt == 1 => throw new TimeoutException("startup ping timed out"),
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => CreateSuccessResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, transportClient.CallCount);
        Assert.Equal(0, processHandle.TerminateCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenLifecycleLockAcquisitionTimesOut_ReturnsIpcTimeoutWithoutLaunchingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "lock-timeout");
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock")),
            new StubUnityIpcTransportClient(_ => CreateSuccessResponse("unused")),
            new StubProjectLifecycleLockProvider((_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Timed out while waiting for lifecycle lock.");
            }),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            CreateUnityProject(scope),
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenUnityExitsAndStaleLockFileExists_ReturnsStartupExitFailureWithCleanupDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-lock-file");
        var unityProject = CreateUnityProject(scope);
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock")),
            new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(
                UnityProjectLockFileProbeResult.Locked(scope.GetPath("UnityProject/Temp/UnityLockfile"))));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartProcessExited, result.ErrorCode);
        Assert.Contains("exited before startup readiness", result.Message, StringComparison.Ordinal);
        Assert.Contains("Stale Unity project lock file was removed", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        Assert.Equal("failed", result.FailureInfo.StartupFailure!.Startup!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Unknown, result.FailureInfo.StartupFailure.Startup.ProcessAction);
        Assert.Equal(0, processHandle.TerminateCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenUnityExitsWithoutLockFile_ReturnsStartupExitFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-unlocked");
        var unityProject = CreateUnityProject(scope);
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock")),
            new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(
                UnityProjectLockFileProbeResult.Unlocked(scope.GetPath("UnityProject/Temp/UnityLockfile"))));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartProcessExited, result.ErrorCode);
        Assert.Contains("exited before startup readiness", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        Assert.Equal("failed", result.FailureInfo.StartupFailure!.Startup!.StartupStatus);
        Assert.Equal(DaemonStartupProcessActionValues.Unknown, result.FailureInfo.StartupFailure.Startup.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenUnityExitsWithCompileErrorLog_ReturnsClassifiedStartupFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-compile-error");
        var unityProject = CreateUnityProject(scope);
        var unityLogPath = scope.GetPath("UnityProject/Logs/Editor.log");
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var logReader = new StubUnityLogReader(UnityLogReadResult.Success(
            """
            COMMAND LINE ARGUMENTS:
            Assets/Scripts/Broken.cs(10,5): error CS0246: The type or namespace name 'MissingType' could not be found
            Scripts have compiler errors.
            """,
            truncated: false,
            path: unityLogPath,
            sizeBytes: 192));
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock")),
            new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(
                UnityProjectLockFileProbeResult.Unlocked(scope.GetPath("UnityProject/Temp/UnityLockfile"))),
            logReader);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.Equal(1, logReader.CallCount);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        var startupFailure = result.FailureInfo.StartupFailure!;
        Assert.Equal("blocked", startupFailure.Startup!.StartupStatus);
        Assert.Equal("compile", startupFailure.Startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupProcessActionValues.Unknown, startupFailure.Startup.ProcessAction);
        Assert.Equal("unityScriptCompilationFailed", startupFailure.Diagnosis!.Reason);
        Assert.Equal("CS0246", startupFailure.Diagnosis.PrimaryDiagnostic!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenUnityExitsWithPackageResolutionLog_ReturnsClassifiedStartupFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-package-resolution");
        var unityProject = CreateUnityProject(scope);
        var unityLogPath = scope.GetPath("UnityProject/Logs/Editor.log");
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var logReader = new StubUnityLogReader(UnityLogReadResult.Success(
            """
            COMMAND LINE ARGUMENTS:
            An error occurred while resolving packages:
            Project has invalid dependencies:
              com.example.missing: No package found for com.example.missing.
            """,
            truncated: false,
            path: unityLogPath,
            sizeBytes: 224));
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock")),
            new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(
                UnityProjectLockFileProbeResult.Unlocked(scope.GetPath("UnityProject/Temp/UnityLockfile"))),
            logReader);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.Equal(1, logReader.CallCount);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        var startupFailure = result.FailureInfo.StartupFailure!;
        Assert.Equal("blocked", startupFailure.Startup!.StartupStatus);
        Assert.Equal("packageResolution", startupFailure.Startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupProcessActionValues.Unknown, startupFailure.Startup.ProcessAction);
        Assert.Equal("unityPackageResolutionFailed", startupFailure.Diagnosis!.Reason);
        Assert.Equal("packageResolution", startupFailure.Diagnosis.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenClassifiedProcessExitHasStaleLockDiagnostic_PreservesCleanupMessage ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-compile-error-stale-lock");
        var unityProject = CreateUnityProject(scope);
        var unityLogPath = scope.GetPath("UnityProject/Logs/Editor.log");
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var logReader = new StubUnityLogReader(UnityLogReadResult.Success(
            """
            COMMAND LINE ARGUMENTS:
            Assets/Scripts/Broken.cs(10,5): error CS0246: The type or namespace name 'MissingType' could not be found
            Scripts have compiler errors.
            """,
            truncated: false,
            path: unityLogPath,
            sizeBytes: 192));
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock")),
            new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(
                UnityProjectLockFileProbeResult.Locked(lockFilePath)),
            logReader);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.Contains("CS0246", result.Message, StringComparison.Ordinal);
        Assert.Contains("Stale Unity project lock file was removed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRequestTransportTimesOut_SendsShutdownBeforeTermination ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "request-timeout");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => throw new TimeoutException("request timed out"),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe());

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Equal(3, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Shutdown, transportClient.Requests[2].Method);
        var bootstrapArguments = Assert.IsType<IpcOneshotBootstrapArguments>(launcher.LastBootstrapArguments);
        Assert.Equal(bootstrapArguments.SessionToken, transportClient.Requests[2].SessionToken);
        Assert.Equal(0, processHandle.TerminateCallCount);
        Assert.Equal(1, processHandle.WaitForExitCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenStartupTimeoutReachesEndpointDuringCleanup_SendsShutdownWithoutTerminatingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-timeout-shutdown");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => throw new TimeoutException("startup ping timed out"),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(),
            cleanupTimeout: TimeSpan.FromMilliseconds(50),
            cleanupRetryDelay: TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        var startupFailure = result.FailureInfo.StartupFailure!;
        Assert.Equal("timeout", startupFailure.Startup!.StartupStatus);
        Assert.Equal("endpointNotRegistered", startupFailure.Startup.StartupBlockingReason);
        Assert.Equal(DaemonStartupProcessActionValues.Unknown, startupFailure.Startup.ProcessAction);
        Assert.Equal("startupFailed", startupFailure.Diagnosis!.Reason);
        Assert.Contains(transportClient.Requests, request => string.Equals(request.Method, IpcMethodNames.Shutdown, StringComparison.Ordinal));
        var bootstrapArguments = Assert.IsType<IpcOneshotBootstrapArguments>(launcher.LastBootstrapArguments);
        Assert.All(
            transportClient.Requests.Where(static request => string.Equals(request.Method, IpcMethodNames.Shutdown, StringComparison.Ordinal)),
            request => Assert.Equal(bootstrapArguments.SessionToken, request.SessionToken));
        Assert.Equal(0, processHandle.TerminateCallCount);
        Assert.Equal(1, processHandle.WaitForExitCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenResponseArrivesBeforeRequestDeadline_WaitsForCleanupExitWithCleanupBudget ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "response-before-deadline-cleanup-budget");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: async cancellationToken =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
            });
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => CreateDelayedSuccessResponse(request.RequestId, TimeSpan.FromMilliseconds(40)),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(),
            cleanupTimeout: TimeSpan.FromMilliseconds(500),
            cleanupRetryDelay: TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, processHandle.WaitForExitCallCount);
        Assert.Equal(0, processHandle.TerminateCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenCleanupShutdownIsAcceptedButProcessDoesNotExit_TerminatesProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "shutdown-accepted-process-stays-alive");
        var unityProject = CreateUnityProject(scope);
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => throw new TimeoutException("request timed out"),
                IpcMethodNames.Shutdown => CreateShutdownResponse(request.RequestId),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = CreateClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(),
            cleanupTimeout: TimeSpan.FromMilliseconds(20),
            cleanupRetryDelay: TimeSpan.FromMilliseconds(1));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Equal(3, transportClient.CallCount);
        Assert.Equal(IpcMethodNames.Shutdown, transportClient.Requests[2].Method);
        var bootstrapArguments = Assert.IsType<IpcOneshotBootstrapArguments>(launcher.LastBootstrapArguments);
        Assert.Equal(bootstrapArguments.SessionToken, transportClient.Requests[2].SessionToken);
        Assert.Equal(1, processHandle.TerminateCallCount);
        Assert.Equal(ProcessTerminationMode.GracefulThenKill, processHandle.LastTerminationPolicy!.Mode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenCleanupTerminatesProcessAndStaleUnityLockFileRemains_ReturnsOriginalFailureWithCleanupDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "request-timeout-stale-lock");
        var unityProject = CreateUnityProject(scope);
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock");
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var transportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => throw new TimeoutException("request timed out"),
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var client = new UnityOneshotIpcClient(
            launcher,
            new StubIpcEndpointResolver(endpoint),
            transportClient,
            new StubProjectLifecycleLockProvider(),
            new StubUnityProjectLockFileProbe(UnityProjectLockFileProbeResult.Locked(lockFilePath)));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Contains("Stale Unity project lock file was removed", result.Message, StringComparison.Ordinal);
        Assert.Contains(lockFilePath, result.Message, StringComparison.Ordinal);
        Assert.Equal(1, processHandle.TerminateCallCount);
    }

    private static UnityOneshotIpcClient CreateClient (
        IUnityBatchmodeProcessLauncher launcher,
        IIpcEndpointResolver endpointResolver,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IUnityProjectLockPreflightService unityProjectLockPreflightService,
        TimeSpan cleanupTimeout,
        TimeSpan cleanupRetryDelay)
    {
        return new UnityOneshotIpcClient(
            launcher,
            endpointResolver,
            transportClient,
            lifecycleLockProvider,
            unityProjectLockPreflightService,
            cleanupTimeout,
            cleanupRetryDelay);
    }

    private static ResolvedUnityProjectContext CreateUnityProject (TestDirectoryScope scope)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: scope.GetPath("UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static JsonElement CreateDispatchPayload ()
    {
        return JsonDocument.Parse("""{"sentinel":"oneshot-payload"}""").RootElement.Clone();
    }

    private static UnityIpcDispatchRequest CreateDispatchRequest ()
    {
        return new UnityIpcDispatchRequest(IpcMethodNames.OpsRead, CreateDispatchPayload());
    }

    private static UnityIpcDispatchRequest CreateOpsReadDispatchRequest (
        bool failFast,
        bool requireReadinessGate)
    {
        return new UnityIpcDispatchRequest(
            IpcMethodNames.OpsRead,
            IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(failFast, requireReadinessGate)));
    }

    private static UnityIpcDispatchRequest CreateReadyPingDispatchRequest (bool failFast)
    {
        return new UnityIpcDispatchRequest(
            IpcMethodNames.Ping,
            IpcPayloadCodec.SerializeToElement(new IpcPingRequest(IpcPingClientVersions.Ready, failFast)));
    }

    private static UnityIpcDispatchRequest CreateCompileDispatchRequest ()
    {
        return new UnityIpcDispatchRequest(
            IpcMethodNames.Compile,
            IpcPayloadCodec.SerializeToElement(new IpcCompileRequest("compile-run-1")),
            [IpcEditorLifecycleStateCodec.CompileFailed, IpcEditorLifecycleStateCodec.SafeMode]);
    }

    private static IpcResponse CreateSuccessResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: EmptyPayload(),
            Errors: Array.Empty<IpcError>());
    }

    private static IpcResponse CreateDelayedSuccessResponse (
        string requestId,
        TimeSpan delay)
    {
        Thread.Sleep(delay);
        return CreateSuccessResponse(requestId);
    }

    private static IpcResponse CreateShutdownResponse (string requestId)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownResponse(
            Accepted: true,
            Message: "Shutdown request accepted."));
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: payload,
            Errors: Array.Empty<IpcError>());
    }

    private static IpcResponse CreatePingResponse (
        string requestId,
        string lifecycleState = IpcEditorLifecycleStateCodec.Ready,
        bool canAcceptExecutionRequests = true,
        string projectFingerprint = "project-fingerprint")
    {
        var payload = IpcPayloadCodec.SerializeToElement(IpcPingResponseTestFactory.Create(
            lifecycleState: lifecycleState,
            canAcceptExecutionRequests: canAcceptExecutionRequests,
            projectFingerprint: projectFingerprint));
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: payload,
            Errors: Array.Empty<IpcError>());
    }

    private sealed class StubUnityBatchmodeProcessLauncher : IUnityBatchmodeProcessLauncher
    {
        private readonly UnityBatchmodeProcessLaunchResult result;

        public StubUnityBatchmodeProcessLauncher (UnityBatchmodeProcessLaunchResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public IpcBatchmodeBootstrapArguments? LastBootstrapArguments { get; private set; }

        public string? LastUnityLogPath { get; private set; }

        public ValueTask<UnityBatchmodeProcessLaunchResult> LaunchAsync (
            ResolvedUnityProjectContext unityProject,
            IpcBatchmodeBootstrapArguments bootstrapArguments,
            string unityLogPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastBootstrapArguments = bootstrapArguments;
            LastUnityLogPath = unityLogPath;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityBatchmodeProcessHandle : IUnityBatchmodeProcessHandle
    {
        private readonly int? exitCode;

        private readonly Func<CancellationToken, Task>? waitForExitBehavior;

        public StubUnityBatchmodeProcessHandle (
            bool hasExited = false,
            int? exitCode = null,
            Func<CancellationToken, Task>? waitForExitBehavior = null)
        {
            HasExited = hasExited;
            this.exitCode = exitCode;
            this.waitForExitBehavior = waitForExitBehavior;
        }

        public int ProcessId => 1234;

        public DateTimeOffset? StartTimeUtc => DateTimeOffset.UtcNow;

        public bool HasExited { get; private set; }

        public int? ExitCode => HasExited ? exitCode ?? 0 : null;

        public int WaitForExitCallCount { get; private set; }

        public int TerminateCallCount { get; private set; }

        public ProcessTerminationPolicy? LastTerminationPolicy { get; private set; }

        public async Task WaitForExitAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WaitForExitCallCount++;
            if (waitForExitBehavior != null)
            {
                await waitForExitBehavior(cancellationToken);
            }

            HasExited = true;
        }

        public Task<ProcessTerminationResult> TerminateAsync (
            ProcessTerminationPolicy? terminationPolicy = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TerminateCallCount++;
            LastTerminationPolicy = terminationPolicy;
            HasExited = true;
            return Task.FromResult(ProcessTerminationResult.GracefulExited);
        }

        public ValueTask DisposeAsync ()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubUnityIpcTransportClient : IUnityIpcTransportClient
    {
        private readonly Func<IpcRequest, IpcResponse> responseFactory;

        public StubUnityIpcTransportClient (Func<IpcRequest, IpcResponse> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public int CallCount { get; private set; }

        public List<IpcRequest> Requests { get; } = new List<IpcRequest>();

        public ValueTask<IpcResponse> SendAsync (
            string storageRoot,
            string projectFingerprint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Requests.Add(request);
            return ValueTask.FromResult(responseFactory(request));
        }
    }

    private sealed class StubIpcEndpointResolver : IIpcEndpointResolver
    {
        private readonly IpcEndpoint endpoint;

        public StubIpcEndpointResolver (IpcEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            return endpoint;
        }
    }

    private sealed class StubUnityLogReader : IUnityLogReader
    {
        private readonly UnityLogReadResult result;

        public StubUnityLogReader (UnityLogReadResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ValueTask<UnityLogReadResult> ReadTailAsync (
            string storageRoot,
            string projectFingerprint,
            int maxBytes = IUnityLogReader.DefaultMaxBytes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityProjectLockFileProbe : IUnityProjectLockFileProbe, IUnityProjectLockPreflightService
    {
        private readonly UnityProjectLockFileProbeResult result;

        public StubUnityProjectLockFileProbe ()
            : this(UnityProjectLockFileProbeResult.Unlocked("/tmp/unity-project/Temp/UnityLockfile"))
        {
        }

        public StubUnityProjectLockFileProbe (UnityProjectLockFileProbeResult result)
        {
            this.result = result;
        }

        public UnityProjectLockFileProbeResult Probe (string unityProjectRoot)
        {
            return result;
        }

        public ValueTask<UnityProjectLockPreflightResult> PrepareForUnityProcessStartAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ConvertProbeResult(result, unityProject));
        }

        public ValueTask<UnityProjectLockPreflightResult> CleanupStaleLockAfterUnityProcessExitAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ConvertPostExitProbeResult(result));
        }

        private static UnityProjectLockPreflightResult ConvertProbeResult (
            UnityProjectLockFileProbeResult result,
            ResolvedUnityProjectContext unityProject)
        {
            if (!result.IsSuccess)
            {
                return UnityProjectLockPreflightResult.InspectionFailed(result.ErrorMessage!);
            }

            if (!result.IsLocked)
            {
                return UnityProjectLockPreflightResult.Unlocked(result.LockFilePath!);
            }

            return UnityProjectLockPreflightResult.ActiveLock(
                result.LockFilePath!,
                UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, result.LockFilePath));
        }

        private static UnityProjectLockPreflightResult ConvertPostExitProbeResult (UnityProjectLockFileProbeResult result)
        {
            if (!result.IsSuccess)
            {
                return UnityProjectLockPreflightResult.InspectionFailed(result.ErrorMessage!);
            }

            if (!result.IsLocked)
            {
                return UnityProjectLockPreflightResult.Unlocked(result.LockFilePath!);
            }

            return UnityProjectLockPreflightResult.StaleLockCleared(result.LockFilePath!);
        }
    }

}
