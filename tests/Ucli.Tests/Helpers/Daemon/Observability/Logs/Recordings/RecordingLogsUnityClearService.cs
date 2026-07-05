using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingLogsUnityClearService : ILogsUnityClearService
{
    private readonly LogsUnityClearServiceResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingLogsUnityClearService (LogsUnityClearServiceResult result)
    {
        this.result = result;
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<LogsUnityClearServiceResult> ExecuteAsync (
        LogsUnityClearServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(request, cancellationToken));
        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        LogsUnityClearServiceRequest Request,
        CancellationToken CancellationToken);
}
