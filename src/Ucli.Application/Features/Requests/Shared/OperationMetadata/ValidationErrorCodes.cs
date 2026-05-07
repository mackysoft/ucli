using System.Reflection;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Defines machine-readable validation error codes for static request validation. </summary>
internal static class ValidationErrorCodes
{
    /// <summary> Gets the error code used when protocolVersion differs from supported value. </summary>
    public static readonly UcliErrorCode ProtocolVersionMismatch = new UcliErrorCode("PROTOCOL_VERSION_MISMATCH");

    /// <summary> Gets the error code used when requestId is not a valid UUID. </summary>
    public static readonly UcliErrorCode RequestIdInvalid = new UcliErrorCode("REQUEST_ID_INVALID");

    /// <summary> Gets the error code used when steps is missing. </summary>
    public static readonly UcliErrorCode StepsRequired = new UcliErrorCode("STEPS_REQUIRED");

    /// <summary> Gets the error code used when stepId is missing. </summary>
    public static readonly UcliErrorCode StepIdRequired = new UcliErrorCode("STEP_ID_REQUIRED");

    /// <summary> Gets the error code used when stepId appears multiple times. </summary>
    public static readonly UcliErrorCode StepIdDuplicated = new UcliErrorCode("STEP_ID_DUPLICATED");

    /// <summary> Gets the error code used when step kind is missing. </summary>
    public static readonly UcliErrorCode StepKindRequired = new UcliErrorCode("STEP_KIND_REQUIRED");

    /// <summary> Gets the error code used when step kind is unsupported. </summary>
    public static readonly UcliErrorCode StepKindInvalid = new UcliErrorCode("STEP_KIND_INVALID");

    /// <summary> Gets the error code used when op name is missing. </summary>
    public static readonly UcliErrorCode OperationNameRequired = new UcliErrorCode("OPERATION_NAME_REQUIRED");

    /// <summary> Gets the error code used when op name is not registered. </summary>
    public static readonly UcliErrorCode OperationNotFound = new UcliErrorCode("OPERATION_NOT_FOUND");

    /// <summary> Gets the error code used when operation is blocked by authorization rules. </summary>
    public static readonly UcliErrorCode OperationNotAllowed = new UcliErrorCode("OPERATION_NOT_ALLOWED");

    /// <summary> Gets the error code used when an op step args object violates the registered schema. </summary>
    public static readonly UcliErrorCode OperationArgsInvalid = new UcliErrorCode("OPERATION_ARGS_INVALID");

    /// <summary> Gets the error code used when an edit step violates DSL constraints. </summary>
    public static readonly UcliErrorCode EditStepInvalid = new UcliErrorCode("EDIT_STEP_INVALID");

    private static readonly IReadOnlySet<UcliErrorCode> AllCodes = CreateAllCodes();

    /// <summary> Returns whether the specified code belongs to static request validation. </summary>
    public static bool Contains (UcliErrorCode code)
    {
        return code.IsValid && AllCodes.Contains(code);
    }

    private static IReadOnlySet<UcliErrorCode> CreateAllCodes ()
    {
        var fields = typeof(ValidationErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
        var codes = new HashSet<UcliErrorCode>();
        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (field is { IsInitOnly: true, FieldType: var fieldType } && fieldType == typeof(UcliErrorCode))
            {
                codes.Add((UcliErrorCode)field.GetValue(null)!);
            }
        }

        return codes;
    }
}
