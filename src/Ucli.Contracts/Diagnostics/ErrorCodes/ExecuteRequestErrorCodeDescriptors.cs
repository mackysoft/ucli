namespace MackySoft.Ucli.Contracts;

internal static class ExecuteRequestErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: ExecuteRequestErrorCodes.RequestIdConflict,
            category: "request",
            summary: "A request-id was reused with different request content.",
            meaning: "The request-id is already associated with another request body, so uCLI cannot safely decide whether the new request is a replay.",
            appliesTo:
            [
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
            ],
            possiblePhases: ["idempotencyCheck"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.No,
            inspect: ["requestId", "payload.requestDigest", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Use a new requestId for changed request content, or resend the exact original request."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InvalidArgument]),

        UcliErrorDescriptorFactory.Create(
            code: ExecuteRequestErrorCodes.OperationContractViolation,
            category: "contract",
            summary: "An operation result violated the operation's declared execution contract.",
            meaning: "Unity executed the operation but the observed result contradicted the operation metadata that uCLI exposes to clients.",
            appliesTo:
            [
                UcliCommandIds.Plan,
                UcliCommandIds.Call,
                UcliCommandIds.Resolve,
                UcliCommandIds.Query,
                UcliCommandIds.Refresh,
            ],
            possiblePhases: ["validate", "plan", "call"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.contractViolations[]", "payload.opResults[]", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect payload.contractViolations[] and report the operation metadata and observed result."),
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
    ];
}
