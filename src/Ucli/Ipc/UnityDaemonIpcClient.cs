using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Ipc;

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
    public async ValueTask<UnityIpcRequestExecutionResult> SendAsync (
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
            return UnityIpcRequestExecutionResult.Failure(message, IpcErrorCodes.InternalError);
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
            return UnityIpcRequestExecutionResult.Success(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return UnityIpcRequestExecutionResult.Failure(
                $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds.",
                CliErrorCodes.IpcTimeout);
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return UnityIpcRequestExecutionResult.Failure(
                $"Unity daemon is not running. {exception.Message}",
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
        }
        catch (Exception exception)
        {
            return UnityIpcRequestExecutionResult.Failure(
                $"Failed to execute Unity daemon IPC request. {exception.Message}",
                IpcErrorCodes.InternalError);
        }
    }
}