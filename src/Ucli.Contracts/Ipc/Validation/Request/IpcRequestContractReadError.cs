namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one request-contract read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="OperationIndex"> The operation index when the error is operation-scoped; otherwise <c>-1</c>. </param>
/// <param name="UnknownPropertyName"> The unknown property name when the error is unknown-property related. </param>
/// <param name="OperationId"> The operation identifier context when available. </param>
/// <param name="DuplicatedOperationId"> The duplicated operation identifier for duplicate-id errors. </param>
/// <param name="JsonStringReadError"> The nested JSON string read error for string-contract violations. </param>
/// <param name="OperationObjectReadErrorKind"> The nested object-property read error for <c>args</c> contract violations. </param>
/// <param name="ExpectationReadError"> The nested expectation read error for <c>expect</c> contract violations. </param>
internal readonly record struct IpcRequestContractReadError (
    IpcRequestContractReadErrorKind Kind,
    int OperationIndex,
    string? UnknownPropertyName,
    string? OperationId,
    string? DuplicatedOperationId,
    JsonStringReadError JsonStringReadError,
    OperationObjectReadErrorKind OperationObjectReadErrorKind,
    ExpectationConstraintReadError ExpectationReadError)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static IpcRequestContractReadError None => new(
        Kind: IpcRequestContractReadErrorKind.None,
        OperationIndex: -1,
        UnknownPropertyName: null,
        OperationId: null,
        DuplicatedOperationId: null,
        JsonStringReadError: JsonStringReadError.None,
        OperationObjectReadErrorKind: default,
        ExpectationReadError: ExpectationConstraintReadError.None);

    /// <summary> Creates one error that indicates request root object mismatch. </summary>
    public static IpcRequestContractReadError RequestMustBeObject ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.RequestMustBeObject,
            OperationIndex: -1,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates one unknown request property. </summary>
    public static IpcRequestContractReadError UnknownRequestProperty (string unknownPropertyName)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.UnknownRequestProperty,
            OperationIndex: -1,
            UnknownPropertyName: unknownPropertyName,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates missing <c>protocolVersion</c>. </summary>
    public static IpcRequestContractReadError ProtocolVersionMissing ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.ProtocolVersionMissing,
            OperationIndex: -1,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates invalid <c>protocolVersion</c> type. </summary>
    public static IpcRequestContractReadError ProtocolVersionTypeMismatch ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch,
            OperationIndex: -1,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates request-id string contract violation. </summary>
    public static IpcRequestContractReadError RequestIdContractViolation (JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.RequestIdContractViolation,
            OperationIndex: -1,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: jsonStringReadError,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates request-id format mismatch. </summary>
    public static IpcRequestContractReadError RequestIdFormatMismatch ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.RequestIdFormatMismatch,
            OperationIndex: -1,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates missing <c>ops</c>. </summary>
    public static IpcRequestContractReadError OperationsMissing ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.OperationsMissing,
            OperationIndex: -1,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates invalid <c>ops</c> type. </summary>
    public static IpcRequestContractReadError OperationsTypeMismatch ()
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.OperationsTypeMismatch,
            OperationIndex: -1,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates non-object operation element. </summary>
    public static IpcRequestContractReadError OperationMustBeObject (int operationIndex)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.OperationMustBeObject,
            OperationIndex: operationIndex,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates unknown operation property. </summary>
    public static IpcRequestContractReadError UnknownOperationProperty (
        int operationIndex,
        string unknownPropertyName)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.UnknownOperationProperty,
            OperationIndex: operationIndex,
            UnknownPropertyName: unknownPropertyName,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates operation-id string contract violation. </summary>
    public static IpcRequestContractReadError OperationIdContractViolation (
        int operationIndex,
        JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.OperationIdContractViolation,
            OperationIndex: operationIndex,
            UnknownPropertyName: null,
            OperationId: null,
            DuplicatedOperationId: null,
            JsonStringReadError: jsonStringReadError,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates operation-name string contract violation. </summary>
    public static IpcRequestContractReadError OperationNameContractViolation (
        int operationIndex,
        string? operationId,
        JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.OperationNameContractViolation,
            OperationIndex: operationIndex,
            UnknownPropertyName: null,
            OperationId: operationId,
            DuplicatedOperationId: null,
            JsonStringReadError: jsonStringReadError,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates operation-args object contract violation. </summary>
    public static IpcRequestContractReadError OperationArgsContractViolation (
        int operationIndex,
        string? operationId,
        OperationObjectReadErrorKind operationObjectReadErrorKind)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.OperationArgsContractViolation,
            OperationIndex: operationIndex,
            UnknownPropertyName: null,
            OperationId: operationId,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: operationObjectReadErrorKind,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates operation-alias string contract violation. </summary>
    public static IpcRequestContractReadError OperationAliasContractViolation (
        int operationIndex,
        string? operationId,
        JsonStringReadError jsonStringReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.OperationAliasContractViolation,
            OperationIndex: operationIndex,
            UnknownPropertyName: null,
            OperationId: operationId,
            DuplicatedOperationId: null,
            JsonStringReadError: jsonStringReadError,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }

    /// <summary> Creates one error that indicates operation expectation contract violation. </summary>
    public static IpcRequestContractReadError OperationExpectationContractViolation (
        int operationIndex,
        string? operationId,
        ExpectationConstraintReadError expectationReadError)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.OperationExpectationContractViolation,
            OperationIndex: operationIndex,
            UnknownPropertyName: null,
            OperationId: operationId,
            DuplicatedOperationId: null,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: expectationReadError);
    }

    /// <summary> Creates one error that indicates duplicated operation-id. </summary>
    public static IpcRequestContractReadError DuplicatedOperationIdError (
        int operationIndex,
        string duplicatedOperationId)
    {
        return new IpcRequestContractReadError(
            Kind: IpcRequestContractReadErrorKind.DuplicatedOperationId,
            OperationIndex: operationIndex,
            UnknownPropertyName: null,
            OperationId: duplicatedOperationId,
            DuplicatedOperationId: duplicatedOperationId,
            JsonStringReadError: JsonStringReadError.None,
            OperationObjectReadErrorKind: default,
            ExpectationReadError: ExpectationConstraintReadError.None);
    }
}