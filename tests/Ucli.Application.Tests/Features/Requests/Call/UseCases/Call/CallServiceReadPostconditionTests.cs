using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.CallServiceTestSupport;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

public sealed class CallServiceReadPostconditionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallResponseIncludesReadPostcondition_PersistsAndExposesIt ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
            OperationPolicy.Advanced);
        var readPostconditionStore = new TestMutationReadPostconditionStore();
        var readPostcondition = ReadPostconditionTestFactory.CreateSceneTreeLite();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcResponseStatus.Ok,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: new IpcExecuteStepId("step-1"),
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                            Phase: IpcExecuteOperationPhase.Call,
                            Applied: true,
                            Changed: true,
                            Touched:
                            [
                                new IpcExecuteTouchedResource(
                                    kind: UcliTouchedResourceKind.Scene,
                                    path: "Assets/Scenes/Main.unity",
                                    assetGuid: null),
                            ]),
                    ],
                    errors: [],
                    planToken: null,
                    readPostcondition: readPostcondition)));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            mutationReadPostconditionStore: readPostconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            new CallCommandInput(
                ProjectPath: ProjectPathTestValues.RepositoryUnityProject,
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.ReadPostcondition);
        MutationReadPostconditionStoreAssert.WrittenSceneTreeLiteRequirement(
            readPostconditionStore,
            expectedStorageRoot: ProjectPathTestValues.RepositoryRoot,
            expectedProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
            expectedScenePath: "Assets/Scenes/Main.unity");
        var requirement = Assert.Single(result.Output.ReadPostcondition!.Requirements);
        Assert.Equal(IpcExecuteReadPostconditionSurface.SceneTreeLite, requirement.Surface);
        Assert.Equal(new UnityScenePath("Assets/Scenes/Main.unity"), requirement.ScenePath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadPostconditionPersistenceFails_ReturnsToolErrorAndPreservesOutput ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
            OperationPolicy.Advanced);
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            WriteResult = MutationReadPostconditionStoreOperationResult.Failure(
                ExecutionError.InternalError("Failed to persist mutation read postcondition.")),
        };
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                ExecuteUnityRequestResponseTestFactory.Create(
                    status: IpcResponseStatus.Ok,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: new IpcExecuteStepId("step-1"),
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                            Phase: IpcExecuteOperationPhase.Call,
                            Applied: true,
                            Changed: true,
                            Touched:
                            [
                                new IpcExecuteTouchedResource(
                                    kind: UcliTouchedResourceKind.Scene,
                                    path: "Assets/Scenes/Main.unity",
                                    assetGuid: null),
                            ]),
                    ],
                    errors: [],
                    planToken: null,
                    readPostcondition: ReadPostconditionTestFactory.CreateSceneTreeLite())));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor,
            mutationReadPostconditionStore: readPostconditionStore);

        var result = await service.ExecuteAsync(
            RequestId,
            new CallCommandInput(
                ProjectPath: ProjectPathTestValues.RepositoryUnityProject,
                Mode: NormalizeMode("oneshot"),
                TimeoutMilliseconds: NormalizeTimeout("1200"),
                PlanToken: null,
                WithPlan: false,
                AllowDangerous: false,
                FailFast: false,
                RequestJson: """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.OpResults);
        Assert.NotNull(result.Output.ReadPostcondition);
        MutationReadPostconditionStoreAssert.WrittenOnceForProject(
            readPostconditionStore,
            expectedStorageRoot: ProjectPathTestValues.RepositoryRoot,
            expectedProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"));
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Equal("Failed to persist mutation read postcondition.", error.Message);
    }
}
