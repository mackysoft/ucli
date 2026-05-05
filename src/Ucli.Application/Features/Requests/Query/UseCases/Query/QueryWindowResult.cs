namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents a bounded query item list and its window metadata. </summary>
internal sealed record QueryWindowResult<T> (
    IReadOnlyList<T> Items,
    QueryWindowInfo Window);
