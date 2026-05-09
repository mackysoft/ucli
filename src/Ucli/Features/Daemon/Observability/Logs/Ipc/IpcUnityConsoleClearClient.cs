using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Implements Unity Editor Console clear over Unity IPC transport. </summary>
internal sealed class IpcUnityConsoleClearClient : IUnityConsoleClearClient
{
    private readonly IUnityIpcTransportClient transportClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcUnityConsoleClearClient" /> class. </summary>
    /// <param name="transportClient"> The shared Unity IPC transport client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    public IpcUnityConsoleClearClient (
        IUnityIpcTransportClient transportClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
    }

    /// <inheritdoc />
    public async ValueTask<UnityConsoleClearClientResult> ClearAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        try
        {
            var sessionTokenResolutionResult = await daemonSessionTokenProvider.ResolveAsync(unityProject, cancellationToken).ConfigureAwait(false);
            if (!sessionTokenResolutionResult.IsSuccess)
            {
                if (sessionTokenResolutionResult.IsSessionNotAvailable)
                {
                    return UnityConsoleClearClientResult.Failure(ExecutionError.InternalError(
                        "Daemon session token is not available."));
                }

                return UnityConsoleClearClientResult.Failure(ExecutionError.InternalError(
                    $"Daemon session token could not be resolved. {sessionTokenResolutionResult.Error!.Message}"));
            }

            var request = IpcUnityConsoleClearRequestCodec.CreateRequest(sessionTokenResolutionResult.Token!);
            var response = await transportClient.SendAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IpcUnityConsoleClearResponseCodec.TryDecode(response, out var decodeError))
            {
                return UnityConsoleClearClientResult.Failure(decodeError!);
            }

            return UnityConsoleClearClientResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return UnityConsoleClearClientResult.Failure(ExecutionError.Timeout(
                $"Unity Console clear request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return UnityConsoleClearClientResult.Failure(ExecutionError.InternalError(
                $"Unity daemon is not running. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return UnityConsoleClearClientResult.Failure(ExecutionError.InternalError(
                $"Failed to clear Unity Console through Unity daemon. {exception.Message}"));
        }
    }
}
