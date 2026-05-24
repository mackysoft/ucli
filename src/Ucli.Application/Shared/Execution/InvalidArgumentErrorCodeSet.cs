using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Defines machine-readable failure codes that map to invalid argument outcomes. </summary>
internal static class InvalidArgumentErrorCodeSet
{
    private static readonly IReadOnlySet<UcliCode> AllCodes = CreateAllCodes();

    /// <summary> Returns whether the specified code represents a caller-correctable invalid argument. </summary>
    public static bool Contains (UcliCode code)
    {
        return code.IsValid && AllCodes.Contains(code);
    }

    private static IReadOnlySet<UcliCode> CreateAllCodes ()
    {
        var codes = new HashSet<UcliCode>(ValidationErrorCodes.All)
        {
            UcliCoreErrorCodes.InvalidArgument,
            PlanTokenErrorCodes.PlanTokenRequired,
            PlanTokenErrorCodes.PlanTokenInvalid,
            PlanTokenErrorCodes.PlanTokenExpired,
            PlanTokenErrorCodes.PlanTokenRequestMismatch,
            PlanTokenErrorCodes.StateChangedSincePlan,
            IpcProtocolErrorCodes.ProtocolVersionMismatch,
            OperationAuthorizationErrorCodes.OperationNotAllowed,
        };

        foreach (var code in ProjectContextErrorCodes.All)
        {
            codes.Add(code);
        }

        foreach (var code in VerifyErrorCodes.All)
        {
            codes.Add(code);
        }

        return codes;
    }
}
