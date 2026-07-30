using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static QueryServiceTestSupport;

public sealed class QueryServiceOperationCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenLiveOperationIsNotRegistered_ReturnsOperationNotFoundBeforeUnityExecution ()
    {
        var operationCatalog = new RecordingOperationCatalog
        {
            Operations = [],
        };
        var service = new QueryService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext)),
            operationCatalog,
            new RecordingReadIndexValidationCatalogResolver(),
            new RecordingAssetSearchLookupAccessService(),
            new RecordingSceneTreeLiteAccessService(),
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(CreateUnityOperation(), failFast: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.InvalidInput, error.Kind);
        Assert.Equal(ValidationErrorCodes.OperationNotFound, error.Code);
        Assert.Contains(UcliPrimitiveOperationNames.CompSchema, error.Message, StringComparison.Ordinal);
        Assert.Single(operationCatalog.ProjectGetAllInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenOperationCatalogLoadTimesOut_ReturnsStructuredQueryFailure ()
    {
        var operationCatalog = new RecordingOperationCatalog
        {
            ProjectGetAllException = OperationCatalogLoadException.Create(
                ApplicationFailure.Timeout(
                    "Catalog discovery timed out.",
                    ExecutionErrorCodes.IpcTimeout,
                    instancePath: null,
                    startupFailure: null),
                "Operation catalog discovery failed."),
        };
        var service = new QueryService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(QueryProjectContext)),
            operationCatalog,
            new RecordingReadIndexValidationCatalogResolver(),
            new RecordingAssetSearchLookupAccessService(),
            new RecordingSceneTreeLiteAccessService(),
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(CreateUnityOperation(), failFast: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.Timeout, error.Kind);
        Assert.Contains(
            "Query could not load operation metadata. Operation catalog discovery failed. Catalog discovery timed out.",
            error.Message,
            StringComparison.Ordinal);
        Assert.Single(operationCatalog.ProjectGetAllInvocations);
    }

    private static QueryUnityOperationRequest CreateUnityOperation ()
    {
        return new QueryUnityOperationRequest(
            CommandName: "query.comp.schema",
            OperationId: new IpcExecuteStepId("comp.schema"),
            OperationName: UcliPrimitiveOperationNames.CompSchema,
            Args: JsonSerializer.SerializeToElement(new
            {
                type = "UnityEngine.Transform, UnityEngine.CoreModule",
            }));
    }
}
