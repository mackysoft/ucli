using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Executes one IPC request through Unity oneshot batchmode startup and shared IPC transport. </summary>
internal sealed class UnityOneshotIpcClient : IUnityIpcClient
{
    private const string StartupProbeClientVersion = "ucli-oneshot-startup";

    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly IUnityBatchmodeProcessLauncher batchmodeProcessLauncher;

    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IUnityIpcTransportClient transportClient;

    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityOneshotIpcClient" /> class. </summary>
    /// <param name="batchmodeProcessLauncher"> The Unity batchmode process launcher dependency. </param>
    /// <param name="endpointResolver"> The IPC endpoint resolver dependency. </param>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    public UnityOneshotIpcClient (
        IUnityBatchmodeProcessLauncher batchmodeProcessLauncher,
        IIpcEndpointResolver endpointResolver,
        IUnityIpcTransportClient transportClient,
        IProjectLifecycleLockProvider lifecycleLockProvider)
    {
        this.batchmodeProcessLauncher = batchmodeProcessLauncher ?? throw new ArgumentNullException(nameof(batchmodeProcessLauncher));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
    }

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        string method,
        JsonElement payload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProject.UnityProjectRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProject.RepositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProject.ProjectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var deadline = ExecutionDeadline.Start(timeout);
        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var endpoint = endpointResolver.Resolve(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);

        try
        {
            if (!deadline.TryGetRemainingTimeout(out var lockTimeout))
            {
                return CreateTimeoutFailure(timeout);
            }

            await using var lifecycleLock = await lifecycleLockProvider.Acquire(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    lockTimeout,
                    cancellationToken)
                .ConfigureAwait(false);

            var unityLogDirectoryPath = Path.GetDirectoryName(unityLogPath);
            if (!string.IsNullOrWhiteSpace(unityLogDirectoryPath))
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(unityLogDirectoryPath);
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return CreateTimeoutFailure(timeout);
            }

            var sessionToken = CreateSessionToken();
            var launchResult = await batchmodeProcessLauncher.Launch(
                    unityProject,
                    new IpcOneshotBootstrapArguments(
                        ParentProcessId: Environment.ProcessId,
                        SessionToken: sessionToken,
                        EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
                        EndpointAddress: endpoint.Address),
                    unityLogPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                return UnityRequestExecutionResult.Failure(
                    launchResult.Error!.Message,
                    ExecutionErrorCodeMapper.ToCode(launchResult.Error.Kind));
            }

            await using var processHandle = launchResult.ProcessHandle!;
            var shouldTerminateProcess = true;
            try
            {
                var startupProbeError = await WaitUntilReachable(
                        unityProject,
                        sessionToken,
                        deadline,
                        processHandle,
                        timeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupProbeError != null)
                {
                    return UnityRequestExecutionResult.Failure(
                        startupProbeError.Message,
                        ExecutionErrorCodeMapper.ToCode(startupProbeError.Kind));
                }

                if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
                {
                    return CreateTimeoutFailure(timeout);
                }

                var response = await transportClient.SendAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        UnityIpcRequestFactory.Create(sessionToken, method, payload),
                        requestTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!deadline.TryGetRemainingTimeout(out var exitTimeout))
                {
                    return CreateTimeoutFailure(timeout);
                }

                var exitWaitError = await WaitForExit(processHandle, exitTimeout, cancellationToken).ConfigureAwait(false);
                if (exitWaitError != null)
                {
                    return UnityRequestExecutionResult.Failure(
                        exitWaitError.Message,
                        ExecutionErrorCodeMapper.ToCode(exitWaitError.Kind));
                }

                shouldTerminateProcess = false;
                return UnityRequestExecutionResult.Success(response);
            }
            finally
            {
                if (shouldTerminateProcess && !processHandle.HasExited)
                {
                    // NOTE:
                    // A launched oneshot child must not outlive a failed request attempt because the shared endpoint
                    // would accept stray follow-up traffic and interfere with immediate retries.
                    await processHandle.Terminate(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return CreateTimeoutFailure(timeout);
        }
        catch (Exception exception)
        {
            return UnityRequestExecutionResult.Failure(
                $"Failed to execute Unity oneshot IPC request. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
    }

    private static UnityRequestExecutionResult CreateTimeoutFailure (TimeSpan timeout)
    {
        return UnityRequestExecutionResult.Failure(
            $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
            ExecutionErrorCodes.IpcTimeout);
    }

    private async ValueTask<ExecutionError?> WaitUntilReachable (
        ResolvedUnityProjectContext unityProject,
        string sessionToken,
        ExecutionDeadline deadline,
        IUnityBatchmodeProcessHandle processHandle,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (processHandle.HasExited)
            {
                var exitCode = processHandle.ExitCode;
                return ExecutionError.InternalError(
                    exitCode is int code
                        ? $"Unity oneshot process exited before startup readiness was confirmed. ExitCode={code}."
                        : "Unity oneshot process exited before startup readiness was confirmed.");
            }

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return ExecutionError.Timeout(
                    $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.");
            }

            var attemptTimeout = remainingTimeout < TimeSpan.FromSeconds(1)
                ? remainingTimeout
                : TimeSpan.FromSeconds(1);
            try
            {
                var pingResponse = await transportClient.SendAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        CreateStartupProbeRequest(sessionToken),
                        attemptTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!DaemonPingResponseCodec.TryDecodePayload(pingResponse, out var payload, out var error))
                {
                    return ExecutionError.InternalError(
                        $"Unity oneshot startup probe returned an invalid response. {error!.Message}");
                }

                _ = payload!;
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsStartupRetryable(exception))
            {
                await Task.Delay(GetRetryDelay(remainingTimeout), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsStartupRetryable (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is IpcConnectTimeoutException or System.Net.Sockets.SocketException;
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        if (remainingTimeout < StartupRetryDelay)
        {
            return remainingTimeout;
        }

        return StartupRetryDelay;
    }

    private static string CreateSessionToken ()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static IpcRequest CreateStartupProbeRequest (string sessionToken)
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcPingRequest(StartupProbeClientVersion));
        return UnityIpcRequestFactory.Create(sessionToken, IpcMethodNames.Ping, payload);
    }

    private static async ValueTask<ExecutionError?> WaitForExit (
        IUnityBatchmodeProcessHandle processHandle,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(timeout);

        try
        {
            await processHandle.WaitForExit(timeoutCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            return ExecutionError.Timeout(
                $"Unity oneshot process did not exit within {timeout.TotalMilliseconds:0} milliseconds after response handling completed.");
        }

        if (processHandle.ExitCode is int exitCode && exitCode != 0)
        {
            return ExecutionError.InternalError(
                $"Unity oneshot process exited with code {exitCode}.");
        }

        return null;
    }
}
