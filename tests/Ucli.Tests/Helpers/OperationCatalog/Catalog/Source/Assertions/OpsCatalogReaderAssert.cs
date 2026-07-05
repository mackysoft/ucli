namespace MackySoft.Ucli.Tests.Helpers.OperationCatalog;

internal static class OpsCatalogReaderAssert
{
    public static RecordingOpsCatalogReader.Invocation ReadRequiresReadinessGate (RecordingOpsCatalogReader reader)
    {
        var invocation = Assert.Single(reader.Invocations);
        Assert.True(invocation.RequireReadinessGate);
        Assert.False(invocation.IncludeEditLoweringOnly);
        return invocation;
    }
}
