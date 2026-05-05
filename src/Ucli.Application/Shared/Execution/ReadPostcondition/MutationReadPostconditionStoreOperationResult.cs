using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Represents one mutation read-postcondition store write outcome. </summary>
/// <param name="Error"> The write failure when unsuccessful. </param>
internal sealed record MutationReadPostconditionStoreOperationResult (
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the store operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful store result. </summary>
    public static MutationReadPostconditionStoreOperationResult Success ()
    {
        return new MutationReadPostconditionStoreOperationResult((ExecutionError?)null);
    }

    /// <summary> Creates a failed store result. </summary>
    public static MutationReadPostconditionStoreOperationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new MutationReadPostconditionStoreOperationResult(error);
    }
}
