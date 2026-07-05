using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

namespace MackySoft.Ucli.Application.Tests;

internal static class OpsCatalogAccessInvocationAssert
{
    public static RecordingOpsCatalogSourceRefreshService.RefreshInvocation SourceRefreshedFromPreflight (
        RecordingOpsCatalogSourceRefreshService sourceRefreshService,
        OpsPreflightContext expectedContext,
        string expectedFallbackReason,
        CancellationToken expectedCancellationToken)
    {
        var invocation = SingleSourceRefreshFromPreflight(
            sourceRefreshService,
            expectedContext,
            expectedCancellationToken);
        Assert.Equal(expectedFallbackReason, invocation.FallbackReason);
        return invocation;
    }

    public static RecordingOpsCatalogSourceRefreshService.RefreshInvocation SourceRefreshedFromPreflightWithReasonContaining (
        RecordingOpsCatalogSourceRefreshService sourceRefreshService,
        OpsPreflightContext expectedContext,
        string expectedFallbackReasonFragment,
        CancellationToken expectedCancellationToken)
    {
        var invocation = SingleSourceRefreshFromPreflight(
            sourceRefreshService,
            expectedContext,
            expectedCancellationToken);
        Assert.Contains(expectedFallbackReasonFragment, invocation.FallbackReason, StringComparison.Ordinal);
        return invocation;
    }

    public static RecordingPersistedOpsCatalogReader.ReadDescribeInvocation PersistedDescribeReadFor (
        RecordingPersistedOpsCatalogReader persistedReader,
        OpsPreflightContext expectedContext,
        string expectedOperationName,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(persistedReader.ReadDescribeInvocations);
        Assert.Same(expectedContext.Context.UnityProject, invocation.UnityProject);
        Assert.Equal(expectedOperationName, invocation.CatalogEntry.Name);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }

    private static RecordingOpsCatalogSourceRefreshService.RefreshInvocation SingleSourceRefreshFromPreflight (
        RecordingOpsCatalogSourceRefreshService sourceRefreshService,
        OpsPreflightContext expectedContext,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(sourceRefreshService.RefreshInvocations);
        Assert.Same(expectedContext.Context.UnityProject, invocation.Project);
        Assert.Same(expectedContext.Context.Config, invocation.Config);
        Assert.Equal(expectedContext.Mode, invocation.Mode);
        Assert.Equal(expectedContext.Timeout, invocation.Timeout);
        Assert.Equal(expectedContext.FailFast, invocation.FailFast);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }
}
