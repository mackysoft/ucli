using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Sends one IPC request through the running Unity daemon. </summary>
internal sealed class UnityDaemonIpcClient : IUnityIpcClient
{
    private readonly IUnityIpcTransportClient transportClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityDaemonIpcClient" /> class. </summary>
    /// <param name="transportClient"> The shared transport client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    public UnityDaemonIpcClient (
        IUnityIpcTransportClient transportClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
    }

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        string method,
        JsonElement payload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var sessionTokenResult = await daemonSessionTokenProvider.Resolve(unityProject, cancellationToken).ConfigureAwait(false);
        if (!sessionTokenResult.IsSuccess)
        {
            var message = sessionTokenResult.IsSessionNotAvailable
                ? "Daemon session token is not available."
                : $"Daemon session token could not be resolved. {sessionTokenResult.Error!.Message}";
            return UnityRequestExecutionResult.Failure(message, UcliCoreErrorCodes.InternalError);
        }

        try
        {
            var response = await transportClient.SendAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    UnityIpcRequestFactory.Create(sessionTokenResult.Token!, method, payload),
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return UnityRequestExecutionResult.Success(UnityRequestResponseFactory.Create(response));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return UnityRequestExecutionResult.Failure(
                $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                ExecutionErrorCodes.IpcTimeout);
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return UnityRequestExecutionResult.Failure(
                $"Unity daemon is not running. {exception.Message}",
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
        }
        catch (Exception exception)
        {
            return UnityRequestExecutionResult.Failure(
                $"Failed to execute Unity daemon IPC request. {exception.Message}",
                UcliCoreErrorCodes.InternalError);
        }
    }
}
