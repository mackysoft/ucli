using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Implements daemon-log reads over Unity IPC transport. </summary>
internal sealed class IpcDaemonLogsClient : IDaemonLogsClient
{
    private readonly IDaemonIpcRequestSender daemonIpcRequestSender;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonLogsClient" /> class. </summary>
    /// <param name="daemonIpcRequestSender"> The daemon IPC request sender dependency. </param>
    public IpcDaemonLogsClient (IDaemonIpcRequestSender daemonIpcRequestSender)
    {
        this.daemonIpcRequestSender = daemonIpcRequestSender ?? throw new ArgumentNullException(nameof(daemonIpcRequestSender));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonLogsClientReadResult> ReadAsync (
        ResolvedUnityProjectContext unityProject,
        IpcDaemonLogsReadRequest query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var sendResult = await daemonIpcRequestSender.SendAsync(
                unityProject,
                sessionToken => IpcDaemonLogsRequestCodec.CreateRequest(query, sessionToken),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sendResult.IsSuccess)
        {
            return DaemonLogsClientReadResult.Failure(ProjectError(sendResult.Error!));
        }

        if (!IpcDaemonLogsResponseCodec.TryDecode(sendResult.Response!, out var payload, out var decodeError))
        {
            return DaemonLogsClientReadResult.Failure(decodeError!);
        }

        return DaemonLogsClientReadResult.Success(payload!);
    }

    private static ExecutionError ProjectError (ExecutionError error)
    {
        return error.Kind == ExecutionErrorKind.Timeout
            ? ExecutionError.Timeout($"Unity daemon logs read request timed out. {error.Message}")
            : error;
    }
}
