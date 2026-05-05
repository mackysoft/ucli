namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Executes typed query command workflows. </summary>
internal interface IQueryService
{
    /// <summary> Executes one typed query command. </summary>
    ValueTask<QueryServiceResult> Execute (
        QueryCommandInput input,
        CancellationToken cancellationToken = default);
}
