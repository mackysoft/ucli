using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class RefreshCommandTestData
{
    public const string ContractViolationMessage = "Operation result violated declared assurance facts.";

    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    public static readonly Guid RequestGuid = Guid.Parse(RequestId);

    public static OperationExecuteResult CreateSuccessResult (
        OperationExecutionReadPostcondition? readPostcondition = null,
        OperationExecutionPostReadSource? postReadSource = null)
    {
        return OperationExecuteResultFactory.Success(
            RequestGuid,
            [
                CreateRefreshOperationResult(),
            ],
            "uCLI refresh completed.",
            readPostcondition,
            project: ProjectIdentityInfoTestFactory.Create(),
            postReadSource: postReadSource);
    }

    public static OperationExecutionOperationResult CreateViolationOperationResult ()
    {
        return CreateRefreshOperationResult(
            touched:
            [
                new OperationExecutionTouchedResource(
                    Kind: UcliTouchedResourceKindNames.Asset,
                    Path: "Assets/Example.txt",
                    Guid: null),
            ]);
    }

    public static OperationExecutionContractViolation CreateContractViolation (
        string expectedFact = "assurance.mayDirty=false",
        string observedResult = "opResults[].changed=true")
    {
        return new OperationExecutionContractViolation(
            OpId: "refresh",
            Operation: UcliPrimitiveOperationNames.ProjectRefresh,
            ExpectedFact: expectedFact,
            ObservedResult: observedResult,
            ApplicationState: IpcExecuteApplicationStateNames.Applied);
    }

    public static OperationExecutionPostReadSource CreateRefreshPostReadSource ()
    {
        return new OperationExecutionPostReadSource(
            IpcExecutePostReadSource.CurrentSchemaVersion,
            [
                new OperationExecutionPostReadSourceStep(
                    OpId: "refresh",
                    SourceKind: IpcExecutePostReadSourceKindNames.Refresh,
                    PlayModeMutation: false,
                    Commit: null,
                    PersistenceExpected: true,
                    ExpectedPostState: IpcExecuteExpectedPostStateNames.Unavailable),
            ]);
    }

    private static OperationExecutionOperationResult CreateRefreshOperationResult (
        IReadOnlyList<OperationExecutionTouchedResource>? touched = null)
    {
        return new OperationExecutionOperationResult(
            OpId: "refresh",
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: IpcExecuteOperationPhaseNames.Call,
            Applied: true,
            Changed: true,
            Touched: touched ??
            [
                new OperationExecutionTouchedResource(
                    Kind: UcliTouchedResourceKindNames.Asset,
                    Path: "Assets/Example.txt",
                    Guid: null),
            ]);
    }
}
