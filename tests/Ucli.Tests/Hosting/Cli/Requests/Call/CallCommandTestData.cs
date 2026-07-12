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
                    CreateGoDescribeOperationResult(IpcExecuteOperationPhaseNames.Call, applied: true),
                ],
                plan: new CallPlanOutput(
                    opResults:
                    [
                        CreateGoDescribeOperationResult(IpcExecuteOperationPhaseNames.Plan, applied: false),
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
                        OpId: "step-1",
                        Op: "edit",
                        Phase: IpcExecuteOperationPhaseNames.Call,
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
                    "step-1"),
            ],
            new CallExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults:
                [
                    CreateViolationOperationResult(IpcExecuteOperationPhaseNames.Call, applied: true),
                ],
                plan: new CallPlanOutput(
                    opResults:
                    [
                        CreateViolationOperationResult(IpcExecuteOperationPhaseNames.Plan, applied: false),
                    ],
                    planToken: "plan-token-1")
                {
                    ContractViolations =
                    [
                        CreateContractViolation(IpcExecuteApplicationStateNames.Indeterminate),
                    ],
                },
                readPostcondition: null)
            {
                ContractViolations =
                [
                    CreateContractViolation(IpcExecuteApplicationStateNames.Applied),
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
        string phase,
        bool applied)
    {
        return new OperationExecutionOperationResult(
            OpId: "step-1",
            Op: UcliPrimitiveOperationNames.GoDescribe,
            Phase: phase,
            Applied: applied,
            Changed: false,
            Touched: []);
    }

    private static OperationExecutionOperationResult CreateViolationOperationResult (
        string phase,
        bool applied)
    {
        return new OperationExecutionOperationResult(
            OpId: "step-1",
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: phase,
            Applied: applied,
            Changed: true,
            Touched:
            [
                new OperationExecutionTouchedResource(
                    Kind: UcliTouchedResourceKindNames.Asset,
                    Path: "Assets/Example.txt",
                    Guid: null),
            ]);
    }

    private static OperationExecutionContractViolation CreateContractViolation (string applicationState)
    {
        return new OperationExecutionContractViolation(
            OpId: "step-1",
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
                    OpId: "step-1",
                    SourceKind: IpcExecutePostReadSourceKindNames.Edit,
                    PlayModeMutation: false,
                    Commit: IpcExecutePostReadCommitNames.Context,
                    PersistenceExpected: true,
                    ExpectedPostState: IpcExecuteExpectedPostStateNames.Deterministic),
            ]);
    }
}
