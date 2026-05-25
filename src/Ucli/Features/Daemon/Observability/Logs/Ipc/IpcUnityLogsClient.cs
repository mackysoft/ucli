using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Implements Unity-log reads over Unity IPC transport. </summary>
internal sealed class IpcUnityLogsClient : IUnityLogsClient
{
    private readonly IDaemonIpcRequestSender daemonIpcRequestSender;

    /// <summary> Initializes a new instance of the <see cref="IpcUnityLogsClient" /> class. </summary>
    /// <param name="daemonIpcRequestSender"> The daemon IPC request sender dependency. </param>
    public IpcUnityLogsClient (IDaemonIpcRequestSender daemonIpcRequestSender)
    {
        this.daemonIpcRequestSender = daemonIpcRequestSender ?? throw new ArgumentNullException(nameof(daemonIpcRequestSender));
    }

    /// <inheritdoc />
    public async ValueTask<UnityLogsClientReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        IpcUnityLogsReadRequest query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var sendResult = await daemonIpcRequestSender.SendAsync(
                unityProject,
                sessionToken => IpcUnityLogsRequestCodec.CreateRequest(query, sessionToken),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sendResult.IsSuccess)
        {
            return UnityLogsClientReadResult.Failure(ProjectError(sendResult.Error!));
        }

        if (!IpcUnityLogsResponseCodec.TryDecode(sendResult.Response!, out var payload, out var decodeError))
        {
            return UnityLogsClientReadResult.Failure(decodeError!);
        }

        return UnityLogsClientReadResult.Success(payload!);
    }

    private static ExecutionError ProjectError (ExecutionError error)
    {
        return error.Kind == ExecutionErrorKind.Timeout
            ? ExecutionError.Timeout($"Unity logs read request timed out. {error.Message}")
            : error;
    }
}
