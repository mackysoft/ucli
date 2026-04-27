namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Represents normalized filter input for one <c>query assets find</c> operation. </summary>
internal sealed record QueryAssetsFindFilter (
    string? TypeId,
    string? PathPrefix,
    string? NameContains);
