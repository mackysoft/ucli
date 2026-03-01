using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_SendsPingRequestWithProbeContract ()
    {
        var unityIpcClient = new StubUnityIpcClient();
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
        var unityIpcClient = new StubUnityIpcClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: cancellationTokenSource.Token);
        });
        Assert.Equal(0, unityIpcClient.CallCount);
        Assert.Equal(0, sessionTokenProvider.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WithProvidedSessionToken_SendsPingWithoutResolvingToken ()
    {
        var unityIpcClient = new StubUnityIpcClient();
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
    public async Task Ping_WhenResponseStatusIsError_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new StubUnityIpcClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusError,
                Array.Empty<IpcError>()));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenResponseContainsErrors_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new StubUnityIpcClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                [
                    new IpcError(
                        Code: IpcErrorCodes.InvalidArgument,
                        Message: "invalid request",
                        OpId: null),
                ]));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenSessionIsNotAvailable_ThrowsDaemonPingResponseExceptionWithSessionTokenRequired ()
    {
        var unityIpcClient = new StubUnityIpcClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.SessionNotAvailable());
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None);
        });

        Assert.Equal(IpcErrorCodes.SessionTokenRequired, exception.ErrorCode);
        Assert.Equal(0, unityIpcClient.CallCount);
        Assert.Equal(1, sessionTokenProvider.CallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)ExecutionErrorKind.InvalidArgument)]
    [InlineData((int)ExecutionErrorKind.InternalError)]
    public async Task Ping_WhenSessionTokenResolutionFailsForLocalError_ThrowsDaemonPingResponseExceptionWithoutTokenErrorCode (int errorKind)
    {
        var unityIpcClient = new StubUnityIpcClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Failure(
            new ExecutionError((ExecutionErrorKind)errorKind, "session token read failed")));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await pingClient.Ping(CreateContext(), DefaultTimeout, cancellationToken: CancellationToken.None);
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
        var unityIpcClient = new StubUnityIpcClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider(DaemonSessionTokenResolutionResult.Success("resolved-token"));
        var pingClient = new IpcDaemonPingClient(unityIpcClient, sessionTokenProvider);
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await pingClient.Ping(CreateContext(), timeout, cancellationToken: CancellationToken.None);
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

    private sealed class StubUnityIpcClient : IUnityIpcClient
    {
        private readonly Func<IpcRequest, IpcResponse> responseFactory;

        public StubUnityIpcClient (Func<IpcRequest, IpcResponse>? responseFactory = null)
        {
            this.responseFactory = responseFactory ?? (request =>
                CreateResponse(
                    request,
                    IpcProtocol.StatusOk,
                    Array.Empty<IpcError>()));
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
        IReadOnlyList<IpcError> errors)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: status,
            Payload: JsonDocument.Parse("{}").RootElement.Clone(),
            Errors: errors);
    }
}
