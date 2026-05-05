namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one typed query operation request. </summary>
internal abstract record QueryOperationRequest (
    string CommandName,
    string OperationId,
    string OperationName);
