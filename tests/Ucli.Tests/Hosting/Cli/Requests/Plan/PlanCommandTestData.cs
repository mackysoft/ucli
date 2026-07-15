using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class PlanCommandTestData
{
    public const string DefaultRequestJson = """{"steps":[]}""";

    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    private static readonly Guid RequestGuid = Guid.Parse(RequestId);

    public const string ContractViolationMessage = "Operation result violated declared assurance facts.";

    public static PlanServiceResult CreateSuccessResult ()
    {
        return PlanServiceResult.Success(
            new PlanExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults:
                [
                    CreateSuccessOperationResult(),
                ],
                readIndex: CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    fallbackReason: "readIndex disabled by mode."),
                planToken: "plan-token-1"),
            "uCLI plan completed.");
    }

    public static PlanServiceResult CreateAllowPlayModeSuccessResult ()
    {
        return PlanServiceResult.Success(
            new PlanExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults: [],
                readIndex: CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    fallbackReason: "Play Mode mutation uses live Unity state."),
                planToken: "plan-token-1"),
            "uCLI plan completed.");
    }

    public static PlanServiceResult CreateContractViolationFailureResult ()
    {
        return PlanServiceResult.Failure(
            ContractViolationMessage,
            [
                ApplicationFailure.FromCode(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    ContractViolationMessage,
                    new IpcExecuteStepId("step-1")),
            ],
            new PlanExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults:
                [
                    CreateViolationOperationResult(),
                ],
                readIndex: CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    fallbackReason: "readIndex disabled by mode."),
                planToken: null)
            {
                ContractViolations =
                [
                    CreateContractViolation(),
                ],
            });
    }

    public static PlanServiceResult CreateStaticValidationFailureResult ()
    {
        return PlanServiceResult.Failure(
            "Static validation failed.",
            [
                ApplicationFailure.InvalidInput(
                    "Operation args are invalid.",
                    ValidationErrorCodes.OperationArgsInvalid,
                    new IpcExecuteStepId("step-1")),
            ],
            new PlanExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults: [],
                readIndex: CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    fallbackReason: null),
                planToken: null));
    }

    public static PlanExecutionOutput CreatePreflightOutput ()
    {
        return new PlanExecutionOutput(
            requestId: RequestGuid,
            project: ProjectIdentityInfoTestFactory.Create(),
            opResults: [],
            readIndex: CreateReadIndexInfo(
                used: false,
                hit: false,
                fallbackReason: "readIndex disabled by mode."),
            planToken: null);
    }

    private static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoSource.Index,
            Freshness: IndexFreshness.Probable,
            GeneratedAtUtc: used
                ? DateTimeOffset.Parse("2026-03-06T00:00:00+00:00")
                : null,
            FallbackReason: fallbackReason);
    }

    private static OperationExecutionOperationResult CreateSuccessOperationResult ()
    {
        return new OperationExecutionOperationResult(
            OpId: new IpcExecuteStepId("step-1"),
            Op: UcliPrimitiveOperationNames.GoDescribe,
            Phase: IpcExecuteOperationPhase.Plan,
            Applied: false,
            Changed: false,
            Touched: []);
    }

    private static OperationExecutionOperationResult CreateViolationOperationResult ()
    {
        return new OperationExecutionOperationResult(
            OpId: new IpcExecuteStepId("step-1"),
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: IpcExecuteOperationPhase.Plan,
            Applied: false,
            Changed: true,
            Touched:
            [
                new OperationExecutionTouchedResource(
                    Kind: UcliTouchedResourceKind.Asset,
                    Path: "Assets/Example.txt",
                    AssetGuid: null),
            ]);
    }

    private static OperationExecutionContractViolation CreateContractViolation ()
    {
        return new OperationExecutionContractViolation(
            OpId: new IpcExecuteStepId("step-1"),
            Operation: UcliPrimitiveOperationNames.ProjectRefresh,
            ExpectedFact: "assurance.mayDirty=false",
            ObservedResult: "opResults[].changed=true",
            ApplicationState: IpcApplicationState.Indeterminate);
    }
}
