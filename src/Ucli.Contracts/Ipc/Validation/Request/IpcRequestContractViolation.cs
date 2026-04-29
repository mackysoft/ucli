namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one normalized request-contract violation. </summary>
/// <param name="Kind"> The normalized machine-readable violation kind. </param>
/// <param name="StepIndex"> The step index for step-scoped violations; otherwise <c>-1</c>. </param>
/// <param name="StepId"> The step identifier context when available. </param>
/// <param name="UnknownPropertyName"> The unknown property name for unknown-property violations. </param>
/// <param name="PropertyPath"> The nested property path when available. </param>
/// <param name="DuplicatedStepId"> The duplicated step identifier for duplicate-id violations. </param>
internal readonly record struct IpcRequestContractViolation (
    IpcRequestContractViolationKind Kind,
    int StepIndex,
    string? StepId,
    string? UnknownPropertyName,
    string? PropertyPath,
    string? DuplicatedStepId)
{
    public static IpcRequestContractViolation None => new(
        Kind: IpcRequestContractViolationKind.None,
        StepIndex: -1,
        StepId: null,
        UnknownPropertyName: null,
        PropertyPath: null,
        DuplicatedStepId: null);
}
