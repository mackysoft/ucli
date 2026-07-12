namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Defines machine-readable validation error codes for static request validation. </summary>
internal static class ValidationErrorCodes
{
    /// <summary> Gets the error code used when steps is missing. </summary>
    public static readonly UcliCode StepsRequired = new UcliCode("STEPS_REQUIRED");

    /// <summary> Gets the error code used when stepId is missing. </summary>
    public static readonly UcliCode StepIdRequired = new UcliCode("STEP_ID_REQUIRED");

    /// <summary> Gets the error code used when stepId appears multiple times. </summary>
    public static readonly UcliCode StepIdDuplicated = new UcliCode("STEP_ID_DUPLICATED");

    /// <summary> Gets the error code used when step kind is missing. </summary>
    public static readonly UcliCode StepKindRequired = new UcliCode("STEP_KIND_REQUIRED");

    /// <summary> Gets the error code used when step kind is unsupported. </summary>
    public static readonly UcliCode StepKindInvalid = new UcliCode("STEP_KIND_INVALID");

    /// <summary> Gets the error code used when op name is missing. </summary>
    public static readonly UcliCode OperationNameRequired = new UcliCode("OPERATION_NAME_REQUIRED");

    /// <summary> Gets the error code used when op name is not registered. </summary>
    public static readonly UcliCode OperationNotFound = new UcliCode("OPERATION_NOT_FOUND");

    /// <summary> Gets the error code used when an op step args object violates the registered schema. </summary>
    public static readonly UcliCode OperationArgsInvalid = new UcliCode("OPERATION_ARGS_INVALID");

    /// <summary> Gets the error code used when an edit step violates DSL constraints. </summary>
    public static readonly UcliCode EditStepInvalid = new UcliCode("EDIT_STEP_INVALID");

    private static readonly IReadOnlySet<UcliCode> AllCodes = CreateAllCodes();

    /// <summary> Gets the error codes owned by static request validation. </summary>
    public static IReadOnlyCollection<UcliCode> All => AllCodes;

    /// <summary> Returns whether the specified code belongs to static request validation. </summary>
    public static bool Contains (UcliCode code)
    {
        return code.IsValid && AllCodes.Contains(code);
    }

    private static IReadOnlySet<UcliCode> CreateAllCodes ()
    {
        return new HashSet<UcliCode>
        {
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
