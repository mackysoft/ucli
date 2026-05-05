using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Implements daemon-log reads over Unity IPC transport. </summary>
internal sealed class IpcDaemonLogsClient : IDaemonLogsClient
{
    private readonly IUnityIpcTransportClient transportClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonLogsClient" /> class. </summary>
    /// <param name="transportClient"> The shared Unity IPC transport client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    public IpcDaemonLogsClient (
        IUnityIpcTransportClient transportClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonLogsClientReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        IpcDaemonLogsReadRequest query,
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
                    return DaemonLogsClientReadResult.Failure(ExecutionError.InternalError(
                        "Daemon session token is not available."));
                }

                return DaemonLogsClientReadResult.Failure(ExecutionError.InternalError(
                    $"Daemon session token could not be resolved. {sessionTokenResolutionResult.Error!.Message}"));
            }

            var request = IpcDaemonLogsRequestCodec.CreateRequest(
                query,
                sessionTokenResolutionResult.Token!);
            var response = await transportClient.SendAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IpcDaemonLogsResponseCodec.TryDecode(response, out var payload, out var decodeError))
            {
                return DaemonLogsClientReadResult.Failure(decodeError!);
            }

            return DaemonLogsClientReadResult.Success(payload!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return DaemonLogsClientReadResult.Failure(ExecutionError.Timeout(
                $"Unity daemon logs read request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return DaemonLogsClientReadResult.Failure(ExecutionError.InternalError(
                $"Unity daemon is not running. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return DaemonLogsClientReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read daemon logs from Unity daemon. {exception.Message}"));
        }
    }
}
