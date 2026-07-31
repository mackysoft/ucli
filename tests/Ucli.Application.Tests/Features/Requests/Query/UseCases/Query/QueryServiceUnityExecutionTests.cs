using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;

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
        var unityRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateUnityResponse(OperationDescriptorDigest)));
        var operationCatalog = CreateOperationCatalog(
            UcliPrimitiveOperationNames.CompSchema,
            OperationDescriptorDigest);
        var service = new QueryService(
            projectContextResolver,
            operationCatalog,
            new RecordingReadIndexValidationCatalogResolver(),
            assetSearchLookupAccessService,
            sceneTreeLiteAccessService,
            unityRequestExecutor);

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
        Assert.Equal(OperationDescriptorDigest, Assert.Single(result.OpResults).OperationDescriptorDigest);

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

        var catalogInvocation = Assert.Single(operationCatalog.ProjectGetAllInvocations);
        Assert.Equal(UnityExecutionMode.Oneshot, catalogInvocation.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), catalogInvocation.Timeout);
        Assert.True(catalogInvocation.FailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityResponseUsesAnotherDescriptorDigest_RejectsResponseAsInternalError ()
    {
        var unityRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateUnityResponse(Sha256Digest.Compute("another query operation descriptor"u8))));
        var service = new QueryService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext)),
            CreateOperationCatalog(UcliPrimitiveOperationNames.CompSchema, OperationDescriptorDigest),
            new RecordingReadIndexValidationCatalogResolver(),
            new RecordingAssetSearchLookupAccessService(),
            new RecordingSceneTreeLiteAccessService(),
            unityRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                new QueryUnityOperationRequest(
                    CommandName: "query.comp.schema",
                    OperationId: new IpcExecuteStepId("comp.schema"),
                    OperationName: UcliPrimitiveOperationNames.CompSchema,
                    Args: JsonSerializer.SerializeToElement(new
                    {
                        type = "UnityEngine.Transform, UnityEngine.CoreModule",
                    })),
                readIndexMode: ReadIndexMode.AllowStale,
                failFast: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.InternalError, error.Kind);
        Assert.Contains(
            "'opResults[0].operationDescriptorDigest' field does not match",
            error.Message,
            StringComparison.Ordinal);
        Assert.Empty(result.OpResults);
    }

    private static UnityRequestResponse CreateUnityResponse (Sha256Digest operationDescriptorDigest)
    {
        return ExecuteUnityRequestResponseTestFactory.Create(
            status: IpcResponseStatus.Ok,
            opResults:
            [
                new IpcExecuteOperationResult(
                    Op: UcliPrimitiveOperationNames.CompSchema,
                    Phase: IpcExecuteOperationPhase.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [],
                    OperationDescriptorDigest: operationDescriptorDigest,
                    Verdict: null,
                    Result: JsonSerializer.SerializeToElement(new
                    {
                        type = "UnityEngine.Transform, UnityEngine.CoreModule",
                    }),
                    Diagnostics: []),
            ],
            errors: [],
            project: CreateUnityResponseProjectIdentity());
    }

    private static UnityProjectIdentity CreateUnityResponseProjectIdentity ()
    {
        return new UnityProjectIdentity(
            projectPath: QueryProjectContext.UnityProject.UnityProjectRoot.Value,
            projectFingerprint: ProjectContextTestFactory.ProjectFingerprint,
            unityVersion: QueryProjectContext.UnityProject.UnityVersion);
    }
}
