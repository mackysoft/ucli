namespace MackySoft.Ucli.Contracts;

internal static class ExecuteRequestErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: ExecuteRequestErrorCodes.RequestIdConflict,
            category: "ipc",
            summary: "An IPC request identifier was reused with different request content.",
            meaning: "The outer IPC request envelope identifier is already associated with another request body, so uCLI cannot safely decide whether the incoming envelope is a replay.",
            appliesTo:
            [
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Eval,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
            ],
            possiblePhases: ["idempotencyCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["errors[].message", "IPC client retry and recovery logs"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Do not change payload.requestId or retry automatically; recurring conflicts indicate that the IPC client reused one transport request identifier for different content."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InternalError]),

        UcliErrorDescriptorFactory.Create(
            code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
            category: "diagnostic",
            summary: "Some GameObjects could not be represented by hierarchyPath selectors.",
            meaning: "uCLI hierarchyPath selectors use '/' as the path separator, so GameObject names containing '/' cannot be represented without ambiguity and are excluded from hierarchy-path matching.",
            appliesTo:
            [
                UcliCommandIds.Call,
                UcliCommandIds.Plan,
                UcliCommandIds.Eval,
                UcliCommandIds.Query,
                UcliCommandIds.Resolve,
            ],
            possiblePhases: ["plan", "call"],
            impliesNotApplied: null,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.opResults[].diagnostics[]", "steps[].args.selector.hierarchyPath", "Unity scene hierarchy"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Rename GameObjects whose names contain '/', then rerun the request."),
            ],
            relatedCodes: []),

        UcliErrorDescriptorFactory.Create(
            code: ExecuteRequestErrorCodes.OperationContractViolation,
            category: "operationContract",
            summary: "An operation result violated declared assurance facts.",
            meaning: "The runtime result contradicted the operation metadata contract, so the failure indicates an operation implementation or metadata mismatch and does not imply that the operation was not applied.",
            appliesTo:
            [
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Eval,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
            ],
            possiblePhases: ["plan", "call"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect:
            [
                "errors[].code",
                "errors[].opId",
                "payload.contractViolations[]",
                "payload.opResults[]",
            ],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: "payload.contractViolations[].applicationState is not 'notApplied'",
                    Action: "Do not retry automatically; inspect the operation implementation or metadata and decide whether manual recovery is needed."),
                new UcliErrorNextActionDescriptor(
                    When: "payload.contractViolations[].applicationState is 'notApplied'",
                    Action: "The operation can be retried after the underlying contract mismatch is understood or fixed."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InternalError]),
    ];
}
