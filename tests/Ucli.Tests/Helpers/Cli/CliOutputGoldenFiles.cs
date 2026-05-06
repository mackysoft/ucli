namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;

internal static class CliOutputGoldenFiles
{
    private const string Root = "tests/Ucli.Tests/GoldenFiles/Json/CliOutput";

    public static string GetPath (
        string commandName,
        string fileName)
    {
        return Path.Combine(Root, commandName, fileName);
    }

    public static JsonGoldenFileNormalization NormalizeGeneratedAtUtc ()
    {
        return new JsonGoldenFileNormalization().NormalizeTimestampProperty(
            "generatedAtUtc",
            "<generatedAtUtc>",
            static timestamp => timestamp.Offset == TimeSpan.Zero,
            "an ISO-8601 UTC timestamp");
    }
}
