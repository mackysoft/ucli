using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingVerifyLogsUnityService : ILogsUnityService
{
    private readonly Func<LogsUnityServiceRequest, Func<IpcUnityLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> resultFactory;
    private readonly List<Invocation> invocations = [];

    public RecordingVerifyLogsUnityService (
        Func<LogsUnityServiceRequest, Func<IpcUnityLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> resultFactory)
    {
        this.resultFactory = resultFactory;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<LogsReadServiceResult> ExecuteAsync (
        LogsUnityServiceRequest request,
        Func<IpcUnityLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(request, cancellationToken));
        return resultFactory(request, onEvent, cancellationToken);
    }

    internal readonly record struct Invocation (
        LogsUnityServiceRequest Request,
        CancellationToken CancellationToken);
}
