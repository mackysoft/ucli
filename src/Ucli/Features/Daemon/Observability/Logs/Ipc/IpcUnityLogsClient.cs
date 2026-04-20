using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Implements Unity-log reads over Unity IPC transport. </summary>
internal sealed class IpcUnityLogsClient : IUnityLogsClient
{
    private readonly IUnityIpcTransportClient transportClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcUnityLogsClient" /> class. </summary>
    /// <param name="transportClient"> The shared Unity IPC transport client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    public IpcUnityLogsClient (
        IUnityIpcTransportClient transportClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
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
            var response = await transportClient.SendAsync(
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