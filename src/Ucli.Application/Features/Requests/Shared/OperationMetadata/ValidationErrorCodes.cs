namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Defines machine-readable validation error codes for static request validation. </summary>
internal static class ValidationErrorCodes
{
    /// <summary> Gets the error code used when requestId is not a valid UUID. </summary>
    public static readonly UcliCodeValue RequestIdInvalid = new UcliCodeValue("REQUEST_ID_INVALID");

    /// <summary> Gets the error code used when steps is missing. </summary>
    public static readonly UcliCodeValue StepsRequired = new UcliCodeValue("STEPS_REQUIRED");

    /// <summary> Gets the error code used when stepId is missing. </summary>
    public static readonly UcliCodeValue StepIdRequired = new UcliCodeValue("STEP_ID_REQUIRED");

    /// <summary> Gets the error code used when stepId appears multiple times. </summary>
    public static readonly UcliCodeValue StepIdDuplicated = new UcliCodeValue("STEP_ID_DUPLICATED");

    /// <summary> Gets the error code used when step kind is missing. </summary>
    public static readonly UcliCodeValue StepKindRequired = new UcliCodeValue("STEP_KIND_REQUIRED");

    /// <summary> Gets the error code used when step kind is unsupported. </summary>
    public static readonly UcliCodeValue StepKindInvalid = new UcliCodeValue("STEP_KIND_INVALID");

    /// <summary> Gets the error code used when op name is missing. </summary>
    public static readonly UcliCodeValue OperationNameRequired = new UcliCodeValue("OPERATION_NAME_REQUIRED");

    /// <summary> Gets the error code used when op name is not registered. </summary>
    public static readonly UcliCodeValue OperationNotFound = new UcliCodeValue("OPERATION_NOT_FOUND");

    /// <summary> Gets the error code used when an op step args object violates the registered schema. </summary>
    public static readonly UcliCodeValue OperationArgsInvalid = new UcliCodeValue("OPERATION_ARGS_INVALID");

    /// <summary> Gets the error code used when an edit step violates DSL constraints. </summary>
    public static readonly UcliCodeValue EditStepInvalid = new UcliCodeValue("EDIT_STEP_INVALID");

    private static readonly IReadOnlySet<UcliCodeValue> AllCodes = CreateAllCodes();

    /// <summary> Gets the error codes owned by static request validation. </summary>
    public static IReadOnlyCollection<UcliCodeValue> All => AllCodes;

    /// <summary> Returns whether the specified code belongs to static request validation. </summary>
    public static bool Contains (UcliCodeValue code)
    {
        return code.IsValid && AllCodes.Contains(code);
    }

    private static IReadOnlySet<UcliCodeValue> CreateAllCodes ()
    {
        return new HashSet<UcliCodeValue>
        {
            RequestIdInvalid,
            StepsRequired,
            StepIdRequired,
            StepIdDuplicated,
            StepKindRequired,
            StepKindInvalid,
            OperationNameRequired,
            OperationNotFound,
            OperationArgsInvalid,
            EditStepInvalid,
        };
    }
}
