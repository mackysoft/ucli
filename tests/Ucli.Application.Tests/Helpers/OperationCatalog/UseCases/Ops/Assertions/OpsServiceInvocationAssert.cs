using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Application.Tests;

internal static class OpsServiceInvocationAssert
{
    public static void ListPreflightFailureReturnedBeforeCatalogRead (
        OpsListServiceResult result,
        RecordingOpsCatalogAccessService catalogAccessService,
        RecordingOpsListResultMapper listResultMapper,
        string expectedMessage,
        UcliCode expectedErrorCode)
    {
        var failed = Assert.IsType<OpsListServiceResult.Failed>(result);
        Assert.Equal(expectedMessage, failed.Error.Message);
        Assert.Equal(expectedErrorCode, failed.Error.Code);
        CatalogListReadAndMappingSkipped(catalogAccessService, listResultMapper);
    }

    public static void InvalidListFilterRejectedBeforePreflight (
        OpsListServiceResult result,
        RecordingOpsPreflightService preflightService,
        RecordingOpsCatalogAccessService catalogAccessService,
        RecordingOpsListResultMapper listResultMapper,
        UcliCode expectedErrorCode)
    {
        var failed = Assert.IsType<OpsListServiceResult.Failed>(result);
        Assert.Equal(expectedErrorCode, failed.Error.Code);
        Assert.Empty(preflightService.Invocations);
        CatalogListReadAndMappingSkipped(catalogAccessService, listResultMapper);
    }

    public static void PreflightRequestedFailFast (RecordingOpsPreflightService preflightService)
    {
        var invocation = Assert.Single(preflightService.Invocations);
        Assert.True(invocation.Input.FailFast);
    }

    public static void CatalogListReadFromPreflight (
        RecordingOpsCatalogAccessService catalogAccessService,
        OpsPreflightContext expectedContext)
    {
        var invocation = Assert.Single(catalogAccessService.ListReadInvocations);
        Assert.Same(expectedContext, invocation.Context);
    }

    public static void CatalogDescribeReadFromPreflight (
        RecordingOpsCatalogAccessService catalogAccessService,
        OpsPreflightContext expectedContext,
        string expectedOperationName)
    {
        var invocation = Assert.Single(catalogAccessService.DescribeInvocations);
        Assert.Same(expectedContext, invocation.Context);
        Assert.Equal(expectedOperationName, invocation.OperationName);
    }

    public static RecordingOpsListResultMapper.Invocation ListMappedFrom (
        RecordingOpsListResultMapper listResultMapper,
        OpsListReadOutput expectedOutput,
        params string[] expectedOperationNames)
    {
        var invocation = Assert.Single(listResultMapper.Invocations);
        Assert.Same(expectedOutput, invocation.Output);
        Assert.Equal(expectedOperationNames, invocation.Operations.Select(static operation => operation.Name).ToArray());
        return invocation;
    }

    public static void DescribeMappedFrom (
        RecordingOpsDescribeResultMapper describeResultMapper,
        OpsDescribeReadOutput expectedOutput)
    {
        var invocation = Assert.Single(describeResultMapper.Invocations);
        Assert.Same(expectedOutput, invocation.Output);
    }

    private static void CatalogListReadAndMappingSkipped (
        RecordingOpsCatalogAccessService catalogAccessService,
        RecordingOpsListResultMapper listResultMapper)
    {
        Assert.Empty(catalogAccessService.ListReadInvocations);
        Assert.Empty(listResultMapper.Invocations);
    }
}
