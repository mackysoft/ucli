using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Represents one mutation read-postcondition read outcome. </summary>
/// <param name="ReadPostcondition"> The persisted postcondition when present. </param>
/// <param name="Error"> The read failure when unsuccessful. </param>
internal sealed record MutationReadPostconditionReadResult (
    OperationExecutionReadPostcondition? ReadPostcondition,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the read succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful read result. </summary>
    public static MutationReadPostconditionReadResult Success (OperationExecutionReadPostcondition? readPostcondition)
    {
        return new MutationReadPostconditionReadResult(readPostcondition, (ExecutionError?)null);
    }

    /// <summary> Creates a failed read result. </summary>
    public static MutationReadPostconditionReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new MutationReadPostconditionReadResult(null, error);
    }
}
