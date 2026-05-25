using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Implements Unity Editor Console clear over Unity IPC transport. </summary>
internal sealed class IpcUnityConsoleClearClient : IUnityConsoleClearClient
{
    private readonly IDaemonIpcRequestSender daemonIpcRequestSender;

    /// <summary> Initializes a new instance of the <see cref="IpcUnityConsoleClearClient" /> class. </summary>
    /// <param name="daemonIpcRequestSender"> The daemon IPC request sender dependency. </param>
    public IpcUnityConsoleClearClient (IDaemonIpcRequestSender daemonIpcRequestSender)
    {
        this.daemonIpcRequestSender = daemonIpcRequestSender ?? throw new ArgumentNullException(nameof(daemonIpcRequestSender));
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

        var sendResult = await daemonIpcRequestSender.SendAsync(
                unityProject,
                IpcUnityConsoleClearRequestCodec.CreateRequest,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!sendResult.IsSuccess)
        {
            return UnityConsoleClearClientResult.Failure(ProjectError(sendResult.Error!));
        }

        if (!IpcUnityConsoleClearResponseCodec.TryDecode(sendResult.Response!, out var decodeError))
        {
            return UnityConsoleClearClientResult.Failure(decodeError!);
        }

        return UnityConsoleClearClientResult.Success();
    }

    private static ExecutionError ProjectError (ExecutionError error)
    {
        return error.Kind == ExecutionErrorKind.Timeout
            ? ExecutionError.Timeout($"Unity Console clear request timed out. {error.Message}")
            : error;
    }
}
