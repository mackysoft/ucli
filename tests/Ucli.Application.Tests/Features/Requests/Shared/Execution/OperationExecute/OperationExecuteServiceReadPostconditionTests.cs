using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.OperationExecute;

public sealed class OperationExecuteServiceReadPostconditionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallResponseIncludesReadPostcondition_PersistsAndReturnsIt ()
    {
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver();
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var readPostconditionStore = new TestMutationReadPostconditionStore();
        var readPostcondition = ReadPostconditionTestFactory.CreateAssetSearch();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            OperationExecuteServiceTestSupport.CreateCallSuccessResult(readPostcondition: readPostcondition));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor,
            readPostconditionStore);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RequestId,
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: UnityExecutionMode.Daemon,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        MutationReadPostconditionStoreAssert.WrittenAssetSearchRequirement(
            readPostconditionStore,
            expectedStorageRoot: "/repo",
            expectedProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
            expectedMinSafeGeneratedAtUtc: readPostcondition.Requirements[0].MinSafeGeneratedAtUtc);
        Assert.NotNull(result.ReadPostcondition);
        var requirement = Assert.Single(result.ReadPostcondition!.Requirements);
        Assert.Equal(IpcExecuteReadPostconditionSurface.AssetSearch, requirement.Surface);
        Assert.Equal(readPostcondition.Requirements[0].MinSafeGeneratedAtUtc, requirement.MinSafeGeneratedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadPostconditionPersistenceFails_ReturnsToolErrorAndPreservesPayload ()
    {
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver();
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            WriteResult = MutationReadPostconditionStoreOperationResult.Failure(
                ExecutionError.InternalError("Failed to persist mutation read postcondition.")),
        };
        var readPostcondition = ReadPostconditionTestFactory.CreateAssetSearch();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            OperationExecuteServiceTestSupport.CreateCallSuccessResult(readPostcondition: readPostcondition));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor,
            readPostconditionStore);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RequestId,
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: UnityExecutionMode.Daemon,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Single(result.OpResults);
        Assert.NotNull(result.ReadPostcondition);
        MutationReadPostconditionStoreAssert.WrittenOnceForProject(
            readPostconditionStore,
            expectedStorageRoot: "/repo",
            expectedProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"));
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Equal("Failed to persist mutation read postcondition.", error.Message);
    }
}
