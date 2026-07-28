namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

internal static class ValidationErrorCodeDescriptors
{
    private static readonly UcliCommand[] RequestCommands =
    [
        UcliCommandIds.Validate,
        UcliCommandIds.Plan,
        UcliCommandIds.Call,
        UcliCommandIds.Eval,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
        UcliCommandIds.Refresh,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        CreateValidationDescriptor(
            ValidationErrorCodes.OperationNotFound,
            "Operation name is not registered.",
            "The request references an operation that is not present in the active operation catalog.",
            ["operationCatalogValidation"]),

        CreateValidationDescriptor(
            ValidationErrorCodes.OperationArgsInvalid,
            "Operation arguments violate the registered schema.",
            "An operation step provides arguments that do not satisfy the operation contract.",
            ["operationContractValidation"]),

        CreateValidationDescriptor(
            ValidationErrorCodes.EditStepInvalid,
            "An edit step violates request DSL constraints.",
            "The request contains an edit step that cannot be lowered to a valid operation call.",
            ["requestParsing", "staticValidation"]),
    ];

    private static UcliErrorDescriptor CreateValidationDescriptor (
        UcliCode code,
        string summary,
        string meaning,
        IReadOnlyList<string> possiblePhases)
    {
        return UcliErrorDescriptorFactory.Create(
            code: code,
            category: "requestValidation",
            summary: summary,
            meaning: meaning,
            appliesTo: RequestCommands,
            possiblePhases: possiblePhases,
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect: ["errors[].code", "errors[].instancePath", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Fix the request JSON or selected operation contract, then rerun the command."),
            ],
            relatedCodes:
            [
                UcliCoreErrorCodes.InvalidArgument,
                IpcProtocolErrorCodes.ProtocolVersionMismatch,
                OperationAuthorizationErrorCodes.OperationNotAllowed,
            ]);
    }
}
