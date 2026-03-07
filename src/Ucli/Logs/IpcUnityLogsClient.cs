using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Logs;

/// <summary> Implements Unity-log reads over Unity IPC transport. </summary>
internal sealed class IpcUnityLogsClient : IUnityLogsClient
{
    private readonly IUnityIpcClient unityIpcClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcUnityLogsClient" /> class. </summary>
    /// <param name="unityIpcClient"> The Unity IPC client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    public IpcUnityLogsClient (
        IUnityIpcClient unityIpcClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.unityIpcClient = unityIpcClient ?? throw new ArgumentNullException(nameof(unityIpcClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
    }

    /// <inheritdoc />
    public async ValueTask<UnityLogsClientReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        IpcUnityLogsReadRequest query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        try
        {
            var sessionTokenResolutionResult = await daemonSessionTokenProvider.Resolve(unityProject, cancellationToken).ConfigureAwait(false);
            if (!sessionTokenResolutionResult.IsSuccess)
            {
                if (sessionTokenResolutionResult.IsSessionNotAvailable)
                {
                    return UnityLogsClientReadResult.Failure(ExecutionError.InternalError(
                        "Daemon session token is not available."));
                }

                return UnityLogsClientReadResult.Failure(ExecutionError.InternalError(
                    $"Daemon session token could not be resolved. {sessionTokenResolutionResult.Error!.Message}"));
            }

            var request = IpcUnityLogsRequestCodec.CreateRequest(
                query,
                sessionTokenResolutionResult.Token!);
            var response = await unityIpcClient.SendAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IpcUnityLogsResponseCodec.TryDecode(response, out var payload, out var decodeError))
            {
                return UnityLogsClientReadResult.Failure(decodeError!);
            }

            return UnityLogsClientReadResult.Success(payload!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return UnityLogsClientReadResult.Failure(ExecutionError.Timeout(
                $"Unity logs read request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return UnityLogsClientReadResult.Failure(ExecutionError.InternalError(
                $"Unity daemon is not running. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return UnityLogsClientReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read Unity logs from Unity daemon. {exception.Message}"));
        }
    }
}