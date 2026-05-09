namespace MackySoft.Ucli.Contracts;

internal static class OperationAuthorizationErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        UcliErrorCodeDescriptorFactory.Create(
            code: OperationAuthorizationErrorCodes.OperationNotAllowed,
            category: "authorization",
            summary: "An operation is blocked by policy or explicit CLI guards.",
            meaning: "The requested operation is known but the current policy does not allow uCLI to execute it.",
            appliesTo:
            [
                UcliCommandIds.Validate,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Refresh,
            ],
            possiblePhases: ["operationAuthorization", "staticValidation", "unityExecution"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].opId", "payload.plan", "payload.opResults", "operationPolicy"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Change the request or policy intentionally before retrying."),
            ],
            relatedCodes:
            [
                UcliCoreErrorCodes.InvalidArgument,
                PlayModeErrorCodes.PlayModePersistenceForbidden,
            ]),
    ];
}
