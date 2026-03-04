using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon shutdown request sending through Unity IPC client. </summary>
internal sealed class DaemonShutdownClient : IDaemonShutdownClient
{
    private readonly IUnityIpcClient unityIpcClient;

    /// <summary> Initializes a new instance of the <see cref="DaemonShutdownClient" /> class. </summary>
    /// <param name="unityIpcClient"> The Unity IPC client dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityIpcClient" /> is <see langword="null" />. </exception>
    public DaemonShutdownClient (IUnityIpcClient unityIpcClient)
    {
        this.unityIpcClient = unityIpcClient ?? throw new ArgumentNullException(nameof(unityIpcClient));
    }

    /// <summary> Sends one shutdown request using persisted daemon session token. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The persisted daemon session metadata. </param>
    /// <param name="timeout"> The IPC timeout used for shutdown request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The shutdown attempt result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonShutdownAttemptResult> SendShutdown (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        try
        {
            var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownRequest("ucli-daemon-stop"));
            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: $"daemon-stop-{Guid.NewGuid():N}",
                SessionToken: session.SessionToken,
                Method: IpcMethodNames.Shutdown,
                Payload: payload);
            var response = await unityIpcClient.SendAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
            {
                if (firstError is not null)
                {
                    if (IsSessionTokenErrorCode(firstError.Code))
                    {
                        return DaemonShutdownAttemptResult.NotRunning();
                    }

                    return DaemonShutdownAttemptResult.Failure(ExecutionError.InternalError(
                        $"Daemon shutdown request failed with error code '{firstError.Code}'."));
                }

                return DaemonShutdownAttemptResult.Failure(ExecutionError.InternalError(
                    $"Daemon shutdown request failed with status '{status}'."));
            }

            return DaemonShutdownAttemptResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return DaemonShutdownAttemptResult.NotRunning();
        }
        catch (Exception exception)
        {
            return DaemonShutdownAttemptResult.Failure(ExecutionError.InternalError(
                $"Failed to send daemon shutdown request. {exception.Message}"));
        }
    }

    /// <summary> Determines whether one error code indicates session-token contract failures. </summary>
    /// <param name="errorCode"> The error code to classify. </param>
    /// <returns> <see langword="true" /> when error code indicates session-token contract failure; otherwise <see langword="false" />. </returns>
    private static bool IsSessionTokenErrorCode (string errorCode)
    {
        return string.Equals(errorCode, IpcErrorCodes.SessionTokenRequired, StringComparison.Ordinal)
            || string.Equals(errorCode, IpcErrorCodes.SessionTokenInvalid, StringComparison.Ordinal);
    }
}