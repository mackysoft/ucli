using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve.Contracts;

namespace MackySoft.Tests;

internal sealed class RecordingResolveService : RecordingCommandService<ResolveCommandInput, ResolveServiceResult>, IResolveService
{
    private readonly List<Guid> requestIds = [];

    public RecordingResolveService (Func<ResolveCommandInput, CancellationToken, ValueTask<ResolveServiceResult>> handler)
        : base(handler)
    {
    }

    public IReadOnlyList<Guid> RequestIds => requestIds;

    public ValueTask<ResolveServiceResult> ExecuteAsync (
        Guid requestId,
        ResolveCommandInput input,
        CancellationToken cancellationToken = default)
    {
        requestIds.Add(requestId);
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
