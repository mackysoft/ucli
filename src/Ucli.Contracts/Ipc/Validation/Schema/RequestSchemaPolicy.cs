namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Defines strictness options used when reading operation-contract fields. </summary>
internal readonly record struct RequestSchemaPolicy (
    bool RequireOperationObject,
    bool RequireOperationId,
    bool RequireOperationName,
    bool RequireNonEmptyOperationId,
    bool RequireNonEmptyOperationName)
{
    /// <summary> Gets policy values used by strict execute-request normalization. </summary>
    public static RequestSchemaPolicy StrictExecute => new(
        RequireOperationObject: true,
        RequireOperationId: true,
        RequireOperationName: true,
        RequireNonEmptyOperationId: true,
        RequireNonEmptyOperationName: true);

    /// <summary> Gets policy values used by permissive preflight parsing. </summary>
    public static RequestSchemaPolicy PermissivePreflight => new(
        RequireOperationObject: false,
        RequireOperationId: false,
        RequireOperationName: false,
        RequireNonEmptyOperationId: false,
        RequireNonEmptyOperationName: false);
}