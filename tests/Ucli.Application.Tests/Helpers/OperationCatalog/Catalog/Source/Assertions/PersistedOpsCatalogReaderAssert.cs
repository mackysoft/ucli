using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

namespace MackySoft.Ucli.Application.Tests;

internal static class PersistedOpsCatalogReaderAssert
{
    public static void MalformedCatalogReturnedBeforeFreshnessObservation (
        PersistedOpsCatalogReadResult result,
        RecordingReadIndexFreshnessEvaluator freshnessEvaluator,
        string expectedMessageFragment)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(PersistedOpsCatalogReadFailureKind.Malformed, result.ReadFailure!.Kind);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.ReadFailure.ErrorCode);
        Assert.Contains(expectedMessageFragment, result.ReadFailure.Message, StringComparison.Ordinal);
        Assert.Empty(freshnessEvaluator.ObserveInvocations);
    }
}
