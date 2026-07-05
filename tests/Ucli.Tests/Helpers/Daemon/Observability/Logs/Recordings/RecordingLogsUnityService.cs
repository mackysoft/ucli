using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingLogsUnityService : ILogsUnityService
{
    private readonly Func<LogsUnityServiceRequest, Func<IpcUnityLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> handler;
    private readonly List<Invocation> invocations = [];

    public RecordingLogsUnityService (
        Func<LogsUnityServiceRequest, Func<IpcUnityLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> handler)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<LogsReadServiceResult> ExecuteAsync (
        LogsUnityServiceRequest request,
        Func<IpcUnityLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(request, onEvent, cancellationToken));
        return handler(request, onEvent, cancellationToken);
    }

    internal readonly record struct Invocation (
        LogsUnityServiceRequest Request,
        Func<IpcUnityLogEvent, string, CancellationToken, ValueTask> OnEvent,
        CancellationToken CancellationToken);
}
