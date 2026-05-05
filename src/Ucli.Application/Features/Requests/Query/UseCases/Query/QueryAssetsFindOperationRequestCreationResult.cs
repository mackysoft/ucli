using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents the result of creating one <c>assets.find</c> operation request. </summary>
internal sealed record QueryAssetsFindOperationRequestCreationResult (
    QueryAssetsFindOperationRequest? Operation,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether operation request creation succeeded. </summary>
    public bool IsSuccess => Operation is not null && Error is null;

    /// <summary> Creates a successful operation request creation result. </summary>
    public static QueryAssetsFindOperationRequestCreationResult Success (QueryAssetsFindOperationRequest operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return new QueryAssetsFindOperationRequestCreationResult(operation, null);
    }

    /// <summary> Creates a failed operation request creation result. </summary>
    public static QueryAssetsFindOperationRequestCreationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new QueryAssetsFindOperationRequestCreationResult(null, error);
    }
}
