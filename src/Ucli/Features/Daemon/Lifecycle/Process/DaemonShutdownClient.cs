using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Implements daemon shutdown request sending through Unity IPC client. </summary>
internal sealed class DaemonShutdownClient : IDaemonShutdownClient
{
    private readonly IUnityIpcTransportClient transportClient;

    /// <summary> Initializes a new instance of the <see cref="DaemonShutdownClient" /> class. </summary>
    /// <param name="transportClient"> The shared Unity IPC transport client dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="transportClient" /> is <see langword="null" />. </exception>
    public DaemonShutdownClient (IUnityIpcTransportClient transportClient)
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
            var response = await transportClient.SendAsync(
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
        catch (TimeoutException exception)
        {
            return DaemonShutdownAttemptResult.Failure(ExecutionError.Timeout(
                $"Timed out while sending daemon shutdown request. {exception.Message}"));
        }
        catch (Exception exception) when (IsNotRunningException(exception))
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

    /// <summary>
    /// Determines whether exception can be treated as not-running without masking timeout semantics.
    /// </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns>
    /// <see langword="true" /> when exception indicates not-running and timeout is not involved;
    /// otherwise <see langword="false" />.
    /// </returns>
    private static bool IsNotRunningException (Exception exception)
    {
        if (HasTimeoutInExceptionChain(exception))
        {
            return false;
        }

        return DaemonProbeExceptionClassifier.IsNotRunning(exception);
    }

    /// <summary> Determines whether timeout exists in exception chain. </summary>
    /// <param name="exception"> The exception to inspect. </param>
    /// <returns> <see langword="true" /> when timeout exists in chain; otherwise <see langword="false" />. </returns>
    private static bool HasTimeoutInExceptionChain (Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
