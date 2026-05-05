namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents query result window metadata emitted beside bounded list results. </summary>
internal sealed record QueryWindowInfo (
    int? Limit,
    string? After,
    string? NextCursor,
    bool IsComplete,
    int TotalCount);
