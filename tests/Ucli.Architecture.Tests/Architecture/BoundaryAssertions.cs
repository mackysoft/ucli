namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class BoundaryAssertions
{
    internal static void AssertAllowedItemsByPath (
        IReadOnlyDictionary<string, string[]> expectedItemsByPath,
        IEnumerable<string> actualPaths,
        Func<string, string[]> readActualItems)
    {
        var orderedActualPaths = actualPaths
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedItemsByPath.Keys.OrderBy(static value => value, StringComparer.Ordinal),
            orderedActualPaths);

        foreach (var (path, expectedItems) in expectedItemsByPath)
        {
            var actualItems = readActualItems(path);
            Assert.Equal(
                expectedItems.OrderBy(static value => value, StringComparer.Ordinal),
                actualItems.OrderBy(static value => value, StringComparer.Ordinal));
        }
    }
}
