namespace MackySoft.Tests;

using System.Text.Json;
using Xunit;

internal static class StdoutJsonParser
{
    public static JsonDocument ParseSinglePrettyPrintedObject (string standardOutput)
    {
        var trimmed = standardOutput.Trim();
        Assert.False(string.IsNullOrWhiteSpace(trimmed), "stdout must contain JSON.");

        var lines = trimmed.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length > 1, "stdout JSON must be pretty-printed with newlines.");

        var json = JsonDocument.Parse(trimmed);
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        return json;
    }
}
