using System.Text.Json;

namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one typed query operation request. </summary>
internal abstract record QueryOperationRequest (
    string CommandName,
    string OperationId,
    string OperationName,
    JsonElement Args);
