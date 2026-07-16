namespace MackySoft.Ucli.Contracts;

internal static class OperationAuthorizationErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: OperationAuthorizationErrorCodes.OperationNotAllowed,
            category: "authorization",
            summary: "An operation is blocked by policy or explicit CLI guards.",
            meaning: "The requested operation is known but the current policy does not allow uCLI to execute it.",
            appliesTo:
            [
                UcliCommandIds.Validate,
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Eval,
                UcliCommandIds.Refresh,
            ],
            possiblePhases: ["operationAuthorization", "staticValidation", "unityExecution"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect: ["errors[].opId", "payload.requestId", "payload.opResults", "operationPolicy"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect errors[].message and errors[].opId to identify the blocked operation, required policy, and current operationPolicy; inspect operationAllowlist when allowlist denial is possible."),
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Change .ucli/config.json intentionally before retrying, or use a command allowed by the current policy."),
            ],
            relatedCodes:
            [
                UcliCoreErrorCodes.InvalidArgument,
                PlayModeErrorCodes.PlayModePersistenceForbidden,
            ]),
    ];
}
