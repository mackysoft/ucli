namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Defines strictness options used when reading operation-contract fields. </summary>
internal readonly record struct OperationContractReadPolicy (
    bool RequireOperationId,
    bool RequireOperationName,
    bool RequireNonEmptyOperationId,
    bool RequireNonEmptyOperationName)
{
    /// <summary> Gets policy values used by strict execute-request normalization. </summary>
    public static OperationContractReadPolicy StrictExecute => new(
        RequireOperationId: true,
        RequireOperationName: true,
        RequireNonEmptyOperationId: true,
        RequireNonEmptyOperationName: true);

    /// <summary> Gets policy values used by permissive preflight parsing. </summary>
    public static OperationContractReadPolicy PermissivePreflight => new(
        RequireOperationId: false,
        RequireOperationName: false,
        RequireNonEmptyOperationId: false,
        RequireNonEmptyOperationName: false);
}
