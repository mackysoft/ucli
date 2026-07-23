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
                    OperationId: new IpcExecuteStepId("comp.schema"),
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
        Assert.Equal(QueryProjectContext.UnityProject.UnityProjectRoot.Value, project.ProjectPath);
        Assert.Equal(ProjectContextTestFactory.ProjectFingerprint, project.ProjectFingerprint);
        Assert.Equal(QueryProjectContext.UnityProject.UnityVersion, project.UnityVersion);
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
            status: IpcResponseStatus.Ok,
            opResults:
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("comp.schema"),
                    Op: UcliPrimitiveOperationNames.CompSchema,
                    Phase: IpcExecuteOperationPhase.Plan,
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
            projectPath: QueryProjectContext.UnityProject.UnityProjectRoot.Value,
            projectFingerprint: ProjectContextTestFactory.ProjectFingerprint,
            unityVersion: QueryProjectContext.UnityProject.UnityVersion);
    }
}
