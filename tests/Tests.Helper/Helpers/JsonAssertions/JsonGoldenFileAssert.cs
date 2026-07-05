namespace MackySoft.Tests;

using System.Text;
using System.Text.Json;
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
        ValidateGoldenJson(goldenPath, expectedJson);

        using var actualDocument = ParseActualJson(actualJson);
        var normalizedActualJson = NormalizeActualText(actualJson);
        normalizedActualJson = normalization?.Apply(actualDocument.RootElement, normalizedActualJson) ?? normalizedActualJson;

        if (!string.Equals(expectedJson, normalizedActualJson, StringComparison.Ordinal))
        {
            throw new XunitException(BuildMismatchMessage(goldenPath, expectedJson, normalizedActualJson));
        }
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

    private static JsonDocument ParseActualJson (string actualJson)
    {
        try
        {
            return StdoutJsonParser.ParseSinglePrettyPrintedObject(actualJson);
        }
        catch (JsonException exception)
        {
            throw new XunitException($"Actual stdout JSON must be a single pretty-printed object.{Environment.NewLine}{exception.Message}");
        }
    }

    private static void ValidateGoldenJson (
        string goldenPath,
        string expectedJson)
    {
        try
        {
            using var document = JsonDocument.Parse(expectedJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new XunitException($"Golden JSON root must be an object: {goldenPath}");
            }
        }
        catch (JsonException exception)
        {
            throw new XunitException($"Golden JSON is invalid: {goldenPath}{Environment.NewLine}{exception.Message}");
        }
    }

    private static string BuildMismatchMessage (
        string goldenPath,
        string expectedJson,
        string actualJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Golden JSON mismatch: {goldenPath}");

        var expectedLines = expectedJson.Split('\n');
        var actualLines = actualJson.Split('\n');
        var maxLineCount = Math.Max(expectedLines.Length, actualLines.Length);
        var mismatchCount = 0;
        for (var i = 0; i < maxLineCount; i++)
        {
            var expectedLine = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var actualLine = i < actualLines.Length ? actualLines[i] : "<missing>";
            if (string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                continue;
            }

            builder.AppendLine($"@@ line {i + 1} @@");
            builder.AppendLine($"- {expectedLine}");
            builder.AppendLine($"+ {actualLine}");
            mismatchCount++;
            if (mismatchCount == 20)
            {
                builder.AppendLine("Diff truncated after 20 mismatched lines.");
                break;
            }
        }

        return builder.ToString();
    }

}
