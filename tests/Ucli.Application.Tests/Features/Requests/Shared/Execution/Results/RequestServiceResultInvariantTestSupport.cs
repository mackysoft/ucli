namespace MackySoft.Ucli.Application.Tests.Execution.Results;

internal static class RequestServiceResultInvariantTestSupport
{
    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    public static readonly UcliCode[] InvalidArgumentErrorCodeValues =
    [
        UcliCoreErrorCodes.InvalidArgument,
        PlanTokenErrorCodes.PlanTokenRequired,
        PlanTokenErrorCodes.PlanTokenInvalid,
        PlanTokenErrorCodes.PlanTokenExpired,
        PlanTokenErrorCodes.PlanTokenRequestMismatch,
        PlanTokenErrorCodes.StateChangedSincePlan,
        ProjectContextErrorCodes.ProjectPathInvalidFormat,
        ProjectContextErrorCodes.ProjectPathNotFound,
        ProjectContextErrorCodes.UnityProjectMarkerMissing,
        IpcProtocolErrorCodes.ProtocolVersionMismatch,
        ValidationErrorCodes.RequestIdInvalid,
        ValidationErrorCodes.StepsRequired,
        ValidationErrorCodes.StepIdRequired,
        ValidationErrorCodes.StepIdDuplicated,
        ValidationErrorCodes.StepKindRequired,
        ValidationErrorCodes.StepKindInvalid,
        ValidationErrorCodes.OperationNameRequired,
        ValidationErrorCodes.OperationNotFound,
        OperationAuthorizationErrorCodes.OperationNotAllowed,
        ValidationErrorCodes.OperationArgsInvalid,
        ValidationErrorCodes.EditStepInvalid,
    ];

    public static IReadOnlyList<ApplicationFailure> CreateErrors ()
    {
        return
        [
            ApplicationFailure.InternalError("Failure message."),
        ];
    }

    public static ReadIndexInfo CreateReadIndexInfo ()
    {
        return new ReadIndexInfo(
            Used: true,
            Hit: true,
            Source: ReadIndexInfoSource.Index,
            Freshness: IndexFreshness.Fresh,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
            FallbackReason: null);
    }
}
