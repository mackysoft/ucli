namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Describes allowed and required fields for one public edit action kind. </summary>
internal sealed record IpcEditStepActionShapeDescriptor (
    IpcEditStepContract.ActionKind Kind,
    HashSet<string> AllowedProperties,
    IReadOnlyList<string> RequiredStringProperties,
    string? MissingRequiredMessage,
    bool RequiresValues = false,
    bool RequiresNonEmptyValues = false);
