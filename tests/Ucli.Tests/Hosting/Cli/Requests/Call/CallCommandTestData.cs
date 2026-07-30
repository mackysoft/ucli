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
                readPostcondition: null,
                postReadSource: null),
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
                    OperationExecutionOperationResult.CreateWithoutVerdict(
                        op: "edit",
                        phase: IpcExecuteOperationPhase.Call,
                        applied: true,
                        changed: true,
                        touched: [],
                        operationDescriptorDigest: null,
                        result: null,
                        diagnostics: []),
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
                ApplicationFailure.ContractViolation(
                    ContractViolationMessage,
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "/opResults/0",
                    startupFailure: null),
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
                readPostcondition: null,
                postReadSource: null)
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
            readPostcondition: null,
            postReadSource: null);
    }

    private static OperationExecutionOperationResult CreateGoDescribeOperationResult (
        IpcExecuteOperationPhase phase,
        bool applied)
    {
        return OperationExecutionOperationResult.CreateWithoutVerdict(
            op: UcliPrimitiveOperationNames.GoDescribe,
            phase,
            applied,
            changed: false,
            touched: [],
            operationDescriptorDigest: RequestCommandResultTestValues.OperationDescriptorDigest,
            result: null,
            diagnostics: []);
    }

    private static OperationExecutionOperationResult CreateViolationOperationResult (
        IpcExecuteOperationPhase phase,
        bool applied)
    {
        return OperationExecutionOperationResult.CreateWithoutVerdict(
            op: UcliPrimitiveOperationNames.ProjectRefresh,
            phase,
            applied,
            changed: true,
            touched:
            [
                new OperationExecutionTouchedResource(
                    Kind: UcliTouchedResourceKind.Asset,
                    Path: "Assets/Example.txt",
                    AssetGuid: null),
            ],
            operationDescriptorDigest: RequestCommandResultTestValues.OperationDescriptorDigest,
            result: null,
            diagnostics: []);
    }

    private static OperationExecutionContractViolation CreateContractViolation (IpcApplicationState applicationState)
    {
        return new OperationExecutionContractViolation(
            InstancePath: "/opResults/0",
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
                    SourceKind: IpcExecutePostReadSourceKind.Edit,
                    PlayModeMutation: false,
                    Commit: IpcExecutePostReadCommit.Context,
                    PersistenceExpected: true,
                    ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
            ]);
    }
}
