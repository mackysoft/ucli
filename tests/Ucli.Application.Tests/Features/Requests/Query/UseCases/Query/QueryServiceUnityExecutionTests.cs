using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static QueryServiceTestSupport;

public sealed class QueryServiceUnityExecutionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityOnlyQuery_SendsQueryExecuteRequest ()
    {
        var projectContextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext));
        var assetSearchLookupAccessService = new RecordingAssetSearchLookupAccessService();
        var sceneTreeLiteAccessService = new RecordingSceneTreeLiteAccessService();
        var unityRequestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateUnityResponse()));
        var service = new QueryService(projectContextResolver, assetSearchLookupAccessService, sceneTreeLiteAccessService, unityRequestExecutor);

        var args = JsonSerializer.SerializeToElement(new
        {
            type = "UnityEngine.Transform, UnityEngine.CoreModule",
        });
        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                new QueryUnityOperationRequest(
                    CommandName: "query.comp.schema",
                    OperationId: "comp.schema",
                    OperationName: UcliPrimitiveOperationNames.CompSchema,
                    Args: args),
                readIndexMode: ReadIndexMode.AllowStale,
                failFast: true),
            CancellationToken.None);

        RequestReadIndexAccessInvocationAssert.UnityOnlyQueryBypassedReadIndexAccess(
            result,
            assetSearchLookupAccessService,
            sceneTreeLiteAccessService);
        Assert.NotNull(result.Project);
        var project = result.Project!;
        Assert.Equal("/unity/ResponseProject", project.ProjectPath);
        Assert.Equal(ProjectFingerprintTestFactory.Create("unity-response-fingerprint"), project.ProjectFingerprint);
        Assert.Equal("7000.0.1f1", project.UnityVersion);
        Assert.Equal(RequestId, result.RequestId);

        var execution = RequestReadIndexAccessInvocationAssert.UnityOperationRequestedOnce(
            unityRequestExecutor,
            UcliCommandIds.Query,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1234),
            expectedFailFast: true,
            expectedOperationId: "comp.schema",
            expectedOperationName: UcliPrimitiveOperationNames.CompSchema);
        var executeRequest = execution.Request;
        Assert.Equal("UnityEngine.Transform, UnityEngine.CoreModule", executeRequest.Args.GetProperty("type").GetString());
    }

    private static UnityRequestResponse CreateUnityResponse ()
    {
        return ExecuteUnityRequestResponseTestFactory.Create(
            status: IpcProtocol.StatusOk,
            opResults:
            [
                new IpcExecuteOperationResult(
                    OpId: "comp.schema",
                    Op: UcliPrimitiveOperationNames.CompSchema,
                    Phase: IpcExecuteOperationPhaseNames.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [])
                {
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        type = "UnityEngine.Transform, UnityEngine.CoreModule",
                    }),
                },
            ],
            errors: [],
            project: CreateUnityResponseProjectIdentity());
    }

    private static IpcProjectIdentity CreateUnityResponseProjectIdentity ()
    {
        return new IpcProjectIdentity(
            ProjectPath: "/unity/ResponseProject",
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("unity-response-fingerprint").ToString(),
            UnityVersion: "7000.0.1f1");
    }
}
