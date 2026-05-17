namespace MackySoft.Ucli.Contracts;

/// <summary> Defines reviewed mutation plan-token error code values. </summary>
public static class PlanTokenErrorCodes
{
    /// <summary> Gets the error code emitted when <c>call</c> requires a plan token but none is provided. </summary>
    public static readonly UcliCodeValue PlanTokenRequired = new("PLAN_TOKEN_REQUIRED");

    /// <summary> Gets the error code emitted when a provided plan token fails structural or signature validation. </summary>
    public static readonly UcliCodeValue PlanTokenInvalid = new("PLAN_TOKEN_INVALID");

    /// <summary> Gets the error code emitted when a provided plan token has expired. </summary>
    public static readonly UcliCodeValue PlanTokenExpired = new("PLAN_TOKEN_EXPIRED");

    /// <summary> Gets the error code emitted when token request digest does not match the current request. </summary>
    public static readonly UcliCodeValue PlanTokenRequestMismatch = new("PLAN_TOKEN_REQUEST_MISMATCH");

    /// <summary> Gets the error code emitted when project state changed since token issuance. </summary>
    public static readonly UcliCodeValue StateChangedSincePlan = new("STATE_CHANGED_SINCE_PLAN");
}
