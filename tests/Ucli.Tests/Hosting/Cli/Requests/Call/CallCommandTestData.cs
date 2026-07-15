using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class CallCommandTestData
{
    public const string ContractViolationMessage = "Operation result violated declared assurance facts.";

    public const string DefaultRequestJson = """{"steps":[]}""";

    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    private static readonly Guid RequestGuid = Guid.Parse(RequestId);

    public static CallServiceResult CreateSuccessResult ()
    {
        return CallServiceResult.Success(
            new CallExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults:
                [
                    CreateGoDescribeOperationResult(IpcExecuteOperationPhase.Call, applied: true),
                ],
                plan: new CallPlanOutput(
                    opResults:
                    [
                        CreateGoDescribeOperationResult(IpcExecuteOperationPhase.Plan, applied: false),
                    ],
                    planToken: "plan-token-1"),
                readPostcondition: null),
            "uCLI call completed.");
    }

    public static CallServiceResult CreatePostReadSourceResult ()
    {
        return CallServiceResult.Success(
            new CallExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults:
                [
                    new OperationExecutionOperationResult(
                        OpId: new IpcExecuteStepId("step-1"),
                        Op: "edit",
                        Phase: IpcExecuteOperationPhase.Call,
                        Applied: true,
                        Changed: true,
                        Touched: []),
                ],
                plan: null,
                readPostcondition: null,
                postReadSource: CreateEditPostReadSource()),
            "uCLI call completed.");
    }

    public static CallServiceResult CreateContractViolationFailureResult ()
    {
        return CallServiceResult.Failure(
            ContractViolationMessage,
            [
                ApplicationFailure.FromCode(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    ContractViolationMessage,
                    new IpcExecuteStepId("step-1")),
            ],
            new CallExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults:
                [
                    CreateViolationOperationResult(IpcExecuteOperationPhase.Call, applied: true),
                ],
                plan: new CallPlanOutput(
                    opResults:
                    [
                        CreateViolationOperationResult(IpcExecuteOperationPhase.Plan, applied: false),
                    ],
                    planToken: "plan-token-1")
                {
                    ContractViolations =
                    [
                        CreateContractViolation(IpcApplicationState.Indeterminate),
                    ],
                },
                readPostcondition: null)
            {
                ContractViolations =
                [
                    CreateContractViolation(IpcApplicationState.Applied),
                ],
            });
    }

    public static CallExecutionOutput CreatePreflightOutput ()
    {
        return new CallExecutionOutput(
            requestId: RequestGuid,
            project: ProjectIdentityInfoTestFactory.Create(),
            opResults: [],
            plan: null,
            readPostcondition: null);
    }

    private static OperationExecutionOperationResult CreateGoDescribeOperationResult (
        IpcExecuteOperationPhase phase,
        bool applied)
    {
        return new OperationExecutionOperationResult(
            OpId: new IpcExecuteStepId("step-1"),
            Op: UcliPrimitiveOperationNames.GoDescribe,
            Phase: phase,
            Applied: applied,
            Changed: false,
            Touched: []);
    }

    private static OperationExecutionOperationResult CreateViolationOperationResult (
        IpcExecuteOperationPhase phase,
        bool applied)
    {
        return new OperationExecutionOperationResult(
            OpId: new IpcExecuteStepId("step-1"),
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: phase,
            Applied: applied,
            Changed: true,
            Touched:
            [
                new OperationExecutionTouchedResource(
                    Kind: UcliTouchedResourceKind.Asset,
                    Path: "Assets/Example.txt",
                    AssetGuid: null),
            ]);
    }

    private static OperationExecutionContractViolation CreateContractViolation (IpcApplicationState applicationState)
    {
        return new OperationExecutionContractViolation(
            OpId: new IpcExecuteStepId("step-1"),
            Operation: UcliPrimitiveOperationNames.ProjectRefresh,
            ExpectedFact: "assurance.mayDirty=false",
            ObservedResult: "opResults[].changed=true",
            ApplicationState: applicationState);
    }

    private static OperationExecutionPostReadSource CreateEditPostReadSource ()
    {
        return new OperationExecutionPostReadSource(
            IpcExecutePostReadSource.CurrentSchemaVersion,
            [
                new OperationExecutionPostReadSourceStep(
                    OpId: new IpcExecuteStepId("step-1"),
                    SourceKind: IpcExecutePostReadSourceKind.Edit,
                    PlayModeMutation: false,
                    Commit: IpcExecutePostReadCommit.Context,
                    PersistenceExpected: true,
                    ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
            ]);
    }
}
