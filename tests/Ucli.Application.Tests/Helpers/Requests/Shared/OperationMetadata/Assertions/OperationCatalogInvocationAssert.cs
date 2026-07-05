using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal static class OperationCatalogInvocationAssert
{
    public static RecordingOpsCatalogReader.Invocation OpsCatalogReadRequestedOnce (
        RecordingOpsCatalogReader reader,
        bool expectedFailFast,
        bool expectedRequireReadinessGate,
        bool expectedIncludeEditLoweringOnly)
    {
        var invocation = Assert.Single(reader.Invocations);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        Assert.Equal(expectedRequireReadinessGate, invocation.RequireReadinessGate);
        Assert.Equal(expectedIncludeEditLoweringOnly, invocation.IncludeEditLoweringOnly);
        return invocation;
    }

    public static RecordingOpsCatalogReader.Invocation OpsCatalogReadRequestedWithTimeout (
        RecordingOpsCatalogReader reader,
        TimeSpan expectedTimeout,
        bool expectedFailFast,
        bool expectedRequireReadinessGate,
        bool expectedIncludeEditLoweringOnly)
    {
        var invocation = OpsCatalogReadRequestedOnce(
            reader,
            expectedFailFast,
            expectedRequireReadinessGate,
            expectedIncludeEditLoweringOnly);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        return invocation;
    }

    public static RecordingOperationCatalogDiscoveryService.Invocation OperationDiscoveryRequestedOnce (
        RecordingOperationCatalogDiscoveryService discoveryService,
        ResolvedUnityProjectContext expectedUnityProject,
        UcliConfig expectedConfig,
        UnityExecutionMode expectedMode,
        TimeSpan? expectedTimeout,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(discoveryService.Invocations);
        Assert.Same(expectedUnityProject, invocation.UnityProject);
        Assert.Same(expectedConfig, invocation.Config);
        Assert.Equal(expectedMode, invocation.Mode);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        return invocation;
    }

    public static RecordingOperationCatalog.ProjectGetAllInvocation ProjectCatalogLoadedOnce (
        RecordingOperationCatalog operationCatalog,
        ResolvedUnityProjectContext expectedUnityProject,
        UcliConfig expectedConfig,
        UnityExecutionMode expectedMode,
        TimeSpan expectedTimeout,
        bool expectedFailFast,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(operationCatalog.ProjectGetAllInvocations);
        Assert.Same(expectedUnityProject, invocation.UnityProject);
        Assert.Same(expectedConfig, invocation.Config);
        Assert.Equal(expectedMode, invocation.Mode);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        Assert.Equal(expectedFailFast, invocation.FailFast);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }
}
