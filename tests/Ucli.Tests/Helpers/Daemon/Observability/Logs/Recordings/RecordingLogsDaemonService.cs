using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingLogsDaemonService : ILogsDaemonService
{
    private readonly Func<LogsDaemonServiceRequest, Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> handler;
    private readonly List<Invocation> invocations = [];

    public RecordingLogsDaemonService (
        Func<LogsDaemonServiceRequest, Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<LogsReadServiceResult> ExecuteAsync (
        LogsDaemonServiceRequest request,
        Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(request, onEvent, cancellationToken));
        return handler(request, onEvent, cancellationToken);
    }

    internal readonly record struct Invocation (
        LogsDaemonServiceRequest Request,
        Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> OnEvent,
        CancellationToken CancellationToken);
}
