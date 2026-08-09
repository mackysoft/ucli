namespace MackySoft.Tests;

internal static class JsonTextNormalization
{
    public static string ExpectedJson (string json)
    {
        var normalizedJson = json
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return normalizedJson.EndsWith('\n')
            ? normalizedJson
            : normalizedJson + '\n';
    }
}
