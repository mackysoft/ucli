using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

namespace MackySoft.Tests;

internal sealed class RecordingQueryService : RecordingCommandService<QueryCommandInput, QueryServiceResult>, IQueryService
{
    public RecordingQueryService (Func<QueryCommandInput, CancellationToken, ValueTask<QueryServiceResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<QueryServiceResult> ExecuteAsync (
        QueryCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
