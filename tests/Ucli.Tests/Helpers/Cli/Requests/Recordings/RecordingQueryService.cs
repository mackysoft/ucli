using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

namespace MackySoft.Tests;

internal sealed class RecordingQueryService : RecordingCommandService<QueryCommandInput, QueryServiceResult>, IQueryService
{
    private readonly List<Guid> requestIds = [];

    public RecordingQueryService (Func<QueryCommandInput, CancellationToken, ValueTask<QueryServiceResult>> handler)
        : base(handler)
    {
    }

    public IReadOnlyList<Guid> RequestIds => requestIds;

    public ValueTask<QueryServiceResult> ExecuteAsync (
        Guid requestId,
        QueryCommandInput input,
        CancellationToken cancellationToken = default)
    {
        requestIds.Add(requestId);
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
