namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class SourceBoundaryAssertions
{
    internal static void AssertNoMarkersInCode (IEnumerable<string> sourceFiles, IReadOnlyCollection<string> forbiddenMarkers)
    {
        Assert.Empty(SourceMarkerDetector.FindMarkersInCode(sourceFiles, forbiddenMarkers));
    }
}
