using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ipc;

/// <summary> Executes one IPC request through Unity oneshot batchmode startup and shared IPC transport. </summary>
internal sealed class UnityOneshotIpcClient : IUnityIpcClient
{
    private const string OneshotSessionToken = "oneshot";

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
    public async ValueTask<UnityIpcRequestExecutionResult> SendAsync (
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
                Directory.CreateDirectory(unityLogDirectoryPath);
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return CreateTimeoutFailure(timeout);
            }

            var launchResult = await batchmodeProcessLauncher.Launch(
                    unityProject,
                    new IpcOneshotBootstrapArguments(
                        ParentProcessId: Environment.ProcessId,
                        EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
                        EndpointAddress: endpoint.Address),
                    unityLogPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                return UnityIpcRequestExecutionResult.Failure(
                    launchResult.Error!.Message,
                    ExecutionErrorKindCodeMapper.ToCode(launchResult.Error.Kind));
            }

            await using var processHandle = launchResult.ProcessHandle!;
            var startupProbeError = await WaitUntilReady(
                    unityProject,
                    deadline,
                    processHandle,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (startupProbeError != null)
            {
                await processHandle.Terminate(CancellationToken.None).ConfigureAwait(false);
                return UnityIpcRequestExecutionResult.Failure(
                    startupProbeError.Message,
                    ExecutionErrorKindCodeMapper.ToCode(startupProbeError.Kind));
            }

            if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
            {
                await processHandle.Terminate(CancellationToken.None).ConfigureAwait(false);
                return CreateTimeoutFailure(timeout);
            }

            var response = await transportClient.SendAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    UnityIpcRequestFactory.Create(OneshotSessionToken, method, payload),
                    requestTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!deadline.TryGetRemainingTimeout(out var exitTimeout))
            {
                await processHandle.Terminate(CancellationToken.None).ConfigureAwait(false);
                return CreateTimeoutFailure(timeout);
            }

            var exitWaitError = await WaitForExit(processHandle, exitTimeout, cancellationToken).ConfigureAwait(false);
            if (exitWaitError != null)
            {
                await processHandle.Terminate(CancellationToken.None).ConfigureAwait(false);
                return UnityIpcRequestExecutionResult.Failure(
                    exitWaitError.Message,
                    ExecutionErrorKindCodeMapper.ToCode(exitWaitError.Kind));
            }

            return UnityIpcRequestExecutionResult.Success(response);
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
            return UnityIpcRequestExecutionResult.Failure(
                $"Failed to execute Unity oneshot IPC request. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
    }

    private static UnityIpcRequestExecutionResult CreateTimeoutFailure (TimeSpan timeout)
    {
        return UnityIpcRequestExecutionResult.Failure(
            $"Unity oneshot IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
            CliErrorCodes.IpcTimeout);
    }

    private async ValueTask<ExecutionError?> WaitUntilReady (
        ResolvedUnityProjectContext unityProject,
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
                        CreateStartupProbeRequest(),
                        attemptTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(pingResponse.Status, IpcProtocol.StatusOk, StringComparison.Ordinal))
                {
                    return ExecutionError.InternalError(
                        "Unity oneshot startup probe returned a non-success response.");
                }

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

    private static IpcRequest CreateStartupProbeRequest ()
    {
        var payload = IpcPayloadCodec.SerializeToElement(new IpcPingRequest(StartupProbeClientVersion));
        return UnityIpcRequestFactory.Create(OneshotSessionToken, IpcMethodNames.Ping, payload);
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
