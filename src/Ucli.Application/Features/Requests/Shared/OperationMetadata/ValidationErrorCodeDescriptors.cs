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
            ValidationErrorCodes.RequestIdInvalid,
            "requestId is not a valid UUID.",
            "The requestId field cannot be used for idempotency because it is missing the required UUID format.",
            ["requestParsing", "staticValidation"]),

        CreateValidationDescriptor(
            ValidationErrorCodes.StepsRequired,
            "steps is missing or empty.",
            "The request does not contain any executable operation steps.",
            ["requestParsing", "staticValidation"]),

        CreateValidationDescriptor(
            ValidationErrorCodes.StepIdRequired,
            "stepId is missing.",
            "At least one operation step lacks the identifier required for reporting and result correlation.",
            ["staticValidation"]),

        CreateValidationDescriptor(
            ValidationErrorCodes.StepIdDuplicated,
            "stepId appears multiple times.",
            "The request contains duplicate step identifiers, so results cannot be correlated unambiguously.",
            ["staticValidation"]),

        CreateValidationDescriptor(
            ValidationErrorCodes.StepKindRequired,
            "step kind is missing.",
            "At least one operation step lacks the kind discriminator required to parse it.",
            ["requestParsing", "staticValidation"]),

        CreateValidationDescriptor(
            ValidationErrorCodes.StepKindInvalid,
            "step kind is unsupported.",
            "At least one operation step declares a kind that this uCLI build does not understand.",
            ["requestParsing", "staticValidation"]),

        CreateValidationDescriptor(
            ValidationErrorCodes.OperationNameRequired,
            "Operation name is missing.",
            "An operation step does not specify the operation contract it wants to execute.",
            ["staticValidation"]),

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
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].code", "errors[].opId", "errors[].message", "payload.requestId"],
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
