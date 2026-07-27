namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Defines machine-readable validation error codes for static request validation. </summary>
internal static class ValidationErrorCodes
{
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
        return code is not null && AllCodes.Contains(code);
    }

    private static IReadOnlySet<UcliCode> CreateAllCodes ()
    {
        return new HashSet<UcliCode>
        {
            OperationNotFound,
            OperationArgsInvalid,
            EditStepInvalid,
        };
    }
}
