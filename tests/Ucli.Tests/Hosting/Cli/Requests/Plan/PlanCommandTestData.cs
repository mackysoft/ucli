using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class PlanCommandTestData
{
    public const string DefaultRequestJson = """{"steps":[]}""";

    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    public const string ContractViolationMessage = "Operation result violated declared assurance facts.";

    public static PlanServiceResult CreateSuccessResult ()
    {
        return PlanServiceResult.Success(
            new PlanExecutionOutput(
                RequestId: RequestId,
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults:
                [
                    CreateSuccessOperationResult(),
                ],
                ReadIndex: CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    fallbackReason: "readIndex disabled by mode."),
                PlanToken: "plan-token-1"),
            "uCLI plan completed.");
    }

    public static PlanServiceResult CreateAllowPlayModeSuccessResult ()
    {
        return PlanServiceResult.Success(
            new PlanExecutionOutput(
                RequestId: RequestId,
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults: [],
                ReadIndex: CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    fallbackReason: "Play Mode mutation uses live Unity state."),
                PlanToken: "plan-token-1"),
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
                    "step-1"),
            ],
            new PlanExecutionOutput(
                RequestId: RequestId,
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults:
                [
                    CreateViolationOperationResult(),
                ],
                ReadIndex: CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    fallbackReason: "readIndex disabled by mode."),
                PlanToken: null)
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
                    "step-1"),
            ],
            new PlanExecutionOutput(
                RequestId: RequestId,
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults: [],
                ReadIndex: CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    fallbackReason: null),
                PlanToken: null));
    }

    public static PlanExecutionOutput CreatePreflightOutput ()
    {
        return new PlanExecutionOutput(
            RequestId: RequestId,
            Project: ProjectIdentityInfoTestFactory.Create(),
            OpResults: [],
            ReadIndex: CreateReadIndexInfo(
                used: false,
                hit: false,
                fallbackReason: "readIndex disabled by mode."),
            PlanToken: null);
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
            OpId: "step-1",
            Op: UcliPrimitiveOperationNames.GoDescribe,
            Phase: IpcExecuteOperationPhaseNames.Plan,
            Applied: false,
            Changed: false,
            Touched: []);
    }

    private static OperationExecutionOperationResult CreateViolationOperationResult ()
    {
        return new OperationExecutionOperationResult(
            OpId: "step-1",
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: IpcExecuteOperationPhaseNames.Plan,
            Applied: false,
            Changed: true,
            Touched:
            [
                new OperationExecutionTouchedResource(
                    Kind: UcliTouchedResourceKindNames.Asset,
                    Path: "Assets/Example.txt",
                    Guid: null),
            ]);
    }

    private static OperationExecutionContractViolation CreateContractViolation ()
    {
        return new OperationExecutionContractViolation(
            OpId: "step-1",
            Operation: UcliPrimitiveOperationNames.ProjectRefresh,
            ExpectedFact: "assurance.mayDirty=false",
            ObservedResult: "opResults[].changed=true",
            ApplicationState: IpcExecuteApplicationStateNames.Indeterminate);
    }
}
