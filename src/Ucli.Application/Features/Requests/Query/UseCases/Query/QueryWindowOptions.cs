namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents normalized query result windowing options. </summary>
internal sealed record QueryWindowOptions (
    bool All,
    int Limit,
    string? After,
    int Offset);
