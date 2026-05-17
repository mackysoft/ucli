namespace MackySoft.Ucli.Contracts;

internal static class PlanTokenErrorCodeDescriptors
{
    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        CreatePlanTokenDescriptor(
            PlanTokenErrorCodes.PlanTokenRequired,
            "A reviewed mutation call requires a plan token.",
            "The call command is configured to require a reviewed plan token, but the request did not provide one.",
            ["planTokenValidation", "argumentParsing"]),

        CreatePlanTokenDescriptor(
            PlanTokenErrorCodes.PlanTokenInvalid,
            "The provided plan token is structurally invalid.",
            "The token cannot be decoded, verified, or matched to the configured token format.",
            ["planTokenValidation"]),

        CreatePlanTokenDescriptor(
            PlanTokenErrorCodes.PlanTokenExpired,
            "The provided plan token has expired.",
            "The token was valid when issued but is no longer within its allowed use window.",
            ["planTokenValidation"]),

        CreatePlanTokenDescriptor(
            PlanTokenErrorCodes.PlanTokenRequestMismatch,
            "The plan token does not match the current request.",
            "The token was issued for different request content and cannot authorize this call.",
            ["planTokenValidation"]),

        CreatePlanTokenDescriptor(
            PlanTokenErrorCodes.StateChangedSincePlan,
            "Project state changed since the plan token was issued.",
            "The token no longer proves that the reviewed plan matches the current project state.",
            ["planTokenValidation", "stateValidation"]),
    ];

    private static UcliErrorDescriptor CreatePlanTokenDescriptor (
        UcliCodeValue code,
        string summary,
        string meaning,
        IReadOnlyList<string> possiblePhases)
    {
        return UcliErrorDescriptorFactory.Create(
            code: code,
            category: "planToken",
            summary: summary,
            meaning: meaning,
            appliesTo: [UcliCommandIds.Call],
            possiblePhases: possiblePhases,
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ReplanRequired,
            inspect: ["payload.requestId", "payload.opResults", "status", UcliErrorInspectTargets.DaemonStatusCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Run plan again and call with the reviewed token through the normal CLI flow."),
            ],
            relatedCodes: new[]
            {
                PlanTokenErrorCodes.PlanTokenRequired,
                PlanTokenErrorCodes.PlanTokenInvalid,
                PlanTokenErrorCodes.PlanTokenExpired,
                PlanTokenErrorCodes.PlanTokenRequestMismatch,
                PlanTokenErrorCodes.StateChangedSincePlan,
            }.Where(relatedCode => relatedCode != code).ToArray());
    }
}
