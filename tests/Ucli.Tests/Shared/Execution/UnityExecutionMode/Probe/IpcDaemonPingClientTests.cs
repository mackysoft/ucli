using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_SendsPingRequestWithProbeContract ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);
        var context = CreateContext();

        await pingClient.Ping(context, DefaultTimeout, cancellationToken: CancellationToken.None);

        Assert.Equal(1, unityIpcClient.CallCount);
        Assert.Equal(1, sessionTokenProvider.CallCount);
        Assert.Equal(context.RepositoryRoot, unityIpcClient.LastStorageRoot);
        Assert.Equal(context.ProjectFingerprint, unityIpcClient.LastProjectFingerprint);
        Assert.Equal(DefaultTimeout, unityIpcClient.LastTimeout);
        var request = Assert.IsType<IpcRequest>(unityIpcClient.LastRequest);
        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal(IpcMethodNames.Ping, request.Method);
        Assert.Equal("resolved-token", request.SessionToken);
        Assert.StartsWith("mode-probe-", request.RequestId, StringComparison.Ordinal);
        Assert.Equal("ucli-mode-probe", request.Payload.GetProperty("clientVersion").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: cancellationTokenSource.Token).AsTask(),
                "Canceled daemon ping",
                AsyncWaitTimeout);
        });
        Assert.Equal(0, unityIpcClient.CallCount);
        Assert.Equal(0, sessionTokenProvider.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WithProvidedSessionToken_SendsPingWithoutResolvingToken ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);
        var context = CreateContext();

        await pingClient.Ping(context, DefaultTimeout, "provided-token", CancellationToken.None);

        Assert.Equal(1, unityIpcClient.CallCount);
        Assert.Equal(0, sessionTokenProvider.CallCount);
        var request = Assert.IsType<IpcRequest>(unityIpcClient.LastRequest);
        Assert.Equal("provided-token", request.SessionToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_ReturnsDecodedPingPayload ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                IpcPingResponseTestFactory.Create(
                    serverVersion: "0.5.0",
                    editorMode: "batchmode",
                    unityVersion: "2022.3.5f1",
                    projectFingerprint: "fingerprint",
                    compileState: "ready")));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var result = await pingClient.PingAndRead(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None);

        Assert.Equal(1, unityIpcClient.CallCount);
        Assert.Equal(1, sessionTokenProvider.CallCount);
        Assert.Equal("0.5.0", result.ServerVersion);
        Assert.Equal("batchmode", result.EditorMode);
        Assert.Equal("2022.3.5f1", result.UnityVersion);
        Assert.Equal("fingerprint", result.ProjectFingerprint);
        Assert.Equal("ready", result.CompileState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenPayloadIsInvalid_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>()));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAndRead(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None).AsTask(),
                "Invalid ping payload result",
                AsyncWaitTimeout);
        });

        Assert.Contains("payload", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenCompileStateIsMissing_ReturnsPayload ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                new
                {
                    serverVersion = "0.5.0",
                    editorMode = "batchmode",
                    unityVersion = "2022.3.5f1",
                    projectFingerprint = "fingerprint",
                }));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var result = await pingClient.PingAndRead(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None);

        Assert.Equal("0.5.0", result.ServerVersion);
        Assert.Equal("batchmode", result.EditorMode);
        Assert.Equal("2022.3.5f1", result.UnityVersion);
        Assert.True(string.IsNullOrWhiteSpace(result.CompileState));
    }

    [Theory]
    [InlineData(nameof(IpcDaemonPingClient.Ping))]
    [InlineData(nameof(IpcDaemonPingClient.PingAndRead))]
    [Trait("Size", "Small")]
    public async Task PingMethods_WhenProjectFingerprintMismatches_ThrowsDaemonPingResponseException (string methodName)
    {
        var unityIpcClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                IpcPingResponseTestFactory.Create(projectFingerprint: "different-fingerprint")));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                InvokePingMethod(pingClient, methodName).AsTask(),
                "Mismatched project fingerprint ping result",
                AsyncWaitTimeout);
        });

        Assert.Contains("projectFingerprint mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenProjectFingerprintValidationIsDisabled_ReturnsMismatchedPayload ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                IpcPingResponseTestFactory.Create(projectFingerprint: "different-fingerprint")));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var result = await pingClient.PingAndRead(
            CreateContext(),
            DefaultTimeout,
            validateProjectFingerprint: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal("different-fingerprint", result.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenResponseStatusIsError_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusError,
                Array.Empty<IpcError>()));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None).AsTask(),
                "Error status ping result",
                AsyncWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenResponseContainsErrors_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                [
                    new IpcError(
                        Code: UcliCoreErrorCodes.InvalidArgument,
                        Message: "invalid request",
                        OpId: null),
                ]));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None).AsTask(),
                "Ping response errors result",
                AsyncWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenSessionIsNotAvailable_ThrowsDaemonPingResponseExceptionWithSessionTokenRequired ()
    {
        var unityIpcClient = new StubUnityIpcTransportClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.SessionNotAvailable());
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None).AsTask(),
                "Missing session token ping result",
                AsyncWaitTimeout);
        });

        Assert.Equal(IpcSessionErrorCodes.SessionTokenRequired, exception.ErrorCode!.Value);
        Assert.Equal(0, unityIpcClient.CallCount);
        Assert.Equal(1, sessionTokenProvider.CallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)ExecutionErrorKind.InvalidArgument)]
    [InlineData((int)ExecutionErrorKind.InternalError)]
    public async Task Ping_WhenSessionTokenResolutionFailsForLocalError_ThrowsDaemonPingResponseExceptionWithoutTokenErrorCode (int errorKind)
    {
        var unityIpcClient = new StubUnityIpcTransportClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Failure(
            new ExecutionError((ExecutionErrorKind)errorKind, "session token read failed")));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None).AsTask(),
                "Session token resolution failure ping result",
                AsyncWaitTimeout);
        });

        Assert.Null(exception.ErrorCode);
        Assert.Contains("session token read failed", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, unityIpcClient.CallCount);
        Assert.Equal(1, sessionTokenProvider.CallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Ping_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var unityIpcClient = new StubUnityIpcTransportClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.Ping(CreateContext(), timeout, cancellationToken: CancellationToken.None).AsTask(),
                "Invalid timeout ping result",
                AsyncWaitTimeout);
        });
        Assert.Equal(0, unityIpcClient.CallCount);
        Assert.Equal(0, sessionTokenProvider.CallCount);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Repo"));
        var projectRoot = Path.Combine(repositoryRoot, "UnityProject");
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static ValueTask InvokePingMethod (
        IpcDaemonPingClient pingClient,
        string methodName)
    {
        return methodName switch
        {
            nameof(IpcDaemonPingClient.Ping) => pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None),
            nameof(IpcDaemonPingClient.PingAndRead) => new ValueTask(pingClient.PingAndRead(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None).AsTask()),
            _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, "Unsupported ping method."),
        };
    }

    private sealed class StubUnityIpcTransportClient : IUnityIpcTransportClient
    {
        private readonly Func<IpcRequest, IpcResponse> responseFactory;

        public StubUnityIpcTransportClient (Func<IpcRequest, IpcResponse>? responseFactory = null)
        {
            this.responseFactory = responseFactory ?? (request =>
                CreateResponse(
                    request,
                    IpcProtocol.StatusOk,
                    Array.Empty<IpcError>(),
                    IpcPingResponseTestFactory.Create(projectFingerprint: "fingerprint")));
        }

        public int CallCount { get; private set; }

        public string? LastStorageRoot { get; private set; }

        public string? LastProjectFingerprint { get; private set; }

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
            LastStorageRoot = storageRoot;
            LastProjectFingerprint = projectFingerprint;
            LastRequest = request;
            LastTimeout = timeout;

            return ValueTask.FromResult(responseFactory(request));
        }
    }

    private sealed class StubDaemonSessionTokenProvider : IDaemonSessionTokenProvider
    {
        private readonly DaemonSessionTokenResolutionResult resolutionResult;

        public StubDaemonSessionTokenProvider (DaemonSessionTokenResolutionResult resolutionResult)
        {
            this.resolutionResult = resolutionResult;
        }

        public int CallCount { get; private set; }

        public ValueTask<DaemonSessionTokenResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(resolutionResult);
        }
    }

    private static IpcResponse CreateResponse (
        IpcRequest request,
        string status,
        IReadOnlyList<IpcError> errors,
        object? payload = null)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: status,
            Payload: payload is null
                ? JsonDocument.Parse("{}").RootElement.Clone()
                : IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors);
    }

}
