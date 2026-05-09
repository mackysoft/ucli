namespace MackySoft.Ucli.Contracts;

internal static class ExecuteRequestErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorCodeDescriptor> All { get; } =
    [
        UcliErrorCodeDescriptorFactory.Create(
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
    ];
}
