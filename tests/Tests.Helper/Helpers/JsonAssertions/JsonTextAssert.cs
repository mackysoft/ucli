namespace MackySoft.Tests;

internal static class JsonTextAssert
{
    public static string ExpectedJson (string json)
    {
        var normalizedJson = NormalizeLineEndings(json);
        return normalizedJson.EndsWith("\n", StringComparison.Ordinal)
            ? normalizedJson
            : normalizedJson + "\n";
    }

    public static void AssertExactJson (
        string expected,
        string actual)
    {
        Assert.DoesNotContain("\r", actual);
        Assert.EndsWith("\n", actual);
        Assert.Equal(expected, actual);
    }

    private static string NormalizeLineEndings (string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
    }
}
