namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one normalized request-contract violation. </summary>
/// <param name="Kind"> The normalized machine-readable violation kind. </param>
/// <param name="OperationIndex"> The operation index for operation-scoped violations; otherwise <c>-1</c>. </param>
/// <param name="OperationId"> The operation identifier context when available. </param>
/// <param name="UnknownPropertyName"> The unknown property name for unknown-property violations. </param>
/// <param name="PropertyPath"> The expectation constraint property path when available. </param>
/// <param name="DuplicatedOperationId"> The duplicated operation identifier for duplicate-id violations. </param>
internal readonly record struct IpcRequestContractViolation (
    IpcRequestContractViolationKind Kind,
    int OperationIndex,
    string? OperationId,
    string? UnknownPropertyName,
    string? PropertyPath,
    string? DuplicatedOperationId)
{
    public static IpcRequestContractViolation None => new(
        Kind: IpcRequestContractViolationKind.None,
        OperationIndex: -1,
        OperationId: null,
        UnknownPropertyName: null,
        PropertyPath: null,
        DuplicatedOperationId: null);
}