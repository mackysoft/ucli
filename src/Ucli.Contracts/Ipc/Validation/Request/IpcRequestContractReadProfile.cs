namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Defines strictness options used when reading execute-request root contracts. </summary>
internal readonly record struct IpcRequestContractReadProfile (
    bool RequireProtocolVersion,
    bool RequireRequestId,
    bool RequireNonEmptyRequestId,
    bool RejectRequestIdOuterWhitespace,
    bool RequireCanonicalRequestIdFormat,
    bool RequireOperations,
    bool RejectDuplicatedOperationId,
    RequestSchemaPolicy OperationSchemaPolicy)
{
    /// <summary> Gets profile values used by strict execute-request normalization. </summary>
    public static IpcRequestContractReadProfile StrictExecute => new(
        RequireProtocolVersion: true,
        RequireRequestId: true,
        RequireNonEmptyRequestId: true,
        RejectRequestIdOuterWhitespace: true,
        RequireCanonicalRequestIdFormat: true,
        RequireOperations: true,
        RejectDuplicatedOperationId: true,
        OperationSchemaPolicy: RequestSchemaPolicy.StrictExecute);

    /// <summary> Gets profile values used by permissive preflight parsing. </summary>
    public static IpcRequestContractReadProfile PermissivePreflight => new(
        RequireProtocolVersion: false,
        RequireRequestId: false,
        RequireNonEmptyRequestId: false,
        RejectRequestIdOuterWhitespace: false,
        RequireCanonicalRequestIdFormat: false,
        RequireOperations: false,
        RejectDuplicatedOperationId: false,
        OperationSchemaPolicy: RequestSchemaPolicy.PermissivePreflight);
}