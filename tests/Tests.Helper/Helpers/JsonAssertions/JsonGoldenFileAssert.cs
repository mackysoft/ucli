namespace MackySoft.Tests;

using Xunit.Sdk;

internal static class JsonGoldenFileAssert
{
    public static void Matches (
        string repositoryRelativeGoldenPath,
        string actualJson,
        JsonGoldenFileNormalization? normalization = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRelativeGoldenPath);

        var goldenPath = TestRepositoryPaths.GetFullPath(repositoryRelativeGoldenPath);
        MatchesFile(goldenPath, actualJson, normalization);
    }

    public static void MatchesFile (
        string goldenPath,
        string actualJson,
        JsonGoldenFileNormalization? normalization = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goldenPath);
        ArgumentNullException.ThrowIfNull(actualJson);

        if (!File.Exists(goldenPath))
        {
            throw new XunitException($"Golden JSON file was not found: {goldenPath}");
        }

        var expectedJson = NormalizeGoldenText(File.ReadAllText(goldenPath));
        using var actualDocument = JsonAssert.ParseMultilineObject(actualJson);
        var normalizedActualJson = NormalizeActualText(actualJson);
        normalizedActualJson = normalization?.Apply(actualDocument.RootElement, normalizedActualJson) ?? normalizedActualJson;
        GoldenJsonAssert.Equal(expectedJson, normalizedActualJson, goldenPath);
    }

    private static string NormalizeActualText (string actualJson)
    {
        if (actualJson.Contains("\r", StringComparison.Ordinal))
        {
            throw new XunitException("Actual JSON must use LF line endings.");
        }

        if (!actualJson.EndsWith('\n'))
        {
            throw new XunitException("Actual JSON must end with a newline.");
        }

        return actualJson;
    }

    private static string NormalizeGoldenText (string goldenJson)
    {
        var normalizedJson = goldenJson
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return normalizedJson.EndsWith('\n')
            ? normalizedJson
            : normalizedJson + "\n";
    }

}
