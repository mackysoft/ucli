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
        return NormalizeProjectIdentity(new JsonGoldenFileNormalization())
            .NormalizeTimestampProperty(
                "generatedAtUtc",
                "<generatedAtUtc>",
                static timestamp => timestamp.Offset == TimeSpan.Zero,
                "an ISO-8601 UTC timestamp");
    }

    public static JsonGoldenFileNormalization NormalizeRequestIds ()
    {
        return NormalizeProjectIdentity(new JsonGoldenFileNormalization())
            .NormalizeGuidProperty("requestId", "<requestId>");
    }

    private static JsonGoldenFileNormalization NormalizeProjectIdentity (JsonGoldenFileNormalization normalization)
    {
        return normalization
            .NormalizeStringPropertyValue(
                "projectPath",
                "<projectPath>",
                static value => Path.IsPathFullyQualified(value),
                "an absolute path")
            .NormalizeStringPropertyValue(
                "projectFingerprint",
                "<projectFingerprint>",
                static value => !string.IsNullOrWhiteSpace(value),
                "a project fingerprint")
            .NormalizeStringPropertyValue(
                "unityVersion",
                "<unityVersion>",
                static value => !string.IsNullOrWhiteSpace(value),
                "a Unity version");
    }
}
