using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Shutdown;

/// <summary> Implements daemon shutdown request sending through Unity IPC client. </summary>
internal sealed class DaemonShutdownClient : IDaemonShutdownClient
{
    private readonly IIpcTransportClient transportClient;

    /// <summary> Initializes a new instance of the <see cref="DaemonShutdownClient" /> class. </summary>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="transportClient" /> is <see langword="null" />. </exception>
    public DaemonShutdownClient (IIpcTransportClient transportClient)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
    }

    /// <summary> Sends one shutdown request using persisted daemon session token. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The persisted daemon session metadata. </param>
    /// <param name="timeout"> The IPC timeout used for shutdown request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The shutdown attempt result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonShutdownAttemptResult> SendShutdownAsync (
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
            if (!DaemonSessionConnectionFactory.TryCreate(session, out var connection, out var connectionError))
            {
                return DaemonShutdownAttemptResult.Failure(connectionError!);
            }

            var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownRequest("ucli-daemon-stop"));
            var request = new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: connection.SessionToken,
                method: IpcMethodNames.Shutdown,
                payload: payload,
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single));
            var response = await transportClient.SendAsync(
                    connection.Endpoint,
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
            {
                if (firstError is not null)
                {
                    if (DaemonProbeExceptionClassifier.IsSessionAuthenticationRejected(firstError.Code))
                    {
                        return DaemonShutdownAttemptResult.Failure(ExecutionError.InternalError(
                            $"Daemon shutdown request was rejected by session authentication. ErrorCode='{firstError.Code}'."));
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
        catch (TimeoutException exception)
        {
            return DaemonShutdownAttemptResult.Failure(ExecutionError.Timeout(
                $"Timed out while sending daemon shutdown request. {exception.Message}"));
        }
        catch (SocketException exception) when (DaemonEndpointAbsenceClassifier.IsDirectEndpointAbsence(exception))
        {
            return DaemonShutdownAttemptResult.NotRunning();
        }
        catch (Exception exception)
        {
            return DaemonShutdownAttemptResult.Failure(ExecutionError.InternalError(
                $"Failed to send daemon shutdown request. {exception.Message}"));
        }
    }

}
