namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Tests;

internal static class CliOutputGoldenFiles
{
    private const string Root = "tests/Ucli.Tests/GoldenFiles/Json/CliOutput";

    private static readonly Lazy<string[]> FullPaths = new(EnumerateFullPathsCore);
    private static readonly Lazy<string[]> RepositoryRelativePaths = new(EnumerateRepositoryRelativePathsCore);
    private static readonly Lazy<GoldenDocument[]> GoldenDocuments = new(ReadGoldenDocuments);

    public static string GetPath (
        string commandName,
        string fileName)
    {
        return Path.Combine(Root, commandName, fileName);
    }

    public static string[] EnumerateFullPaths ()
    {
        return FullPaths.Value.ToArray();
    }

    public static string[] EnumerateRepositoryRelativePaths ()
    {
        return RepositoryRelativePaths.Value.ToArray();
    }

    public static IReadOnlyList<GoldenDocument> ReadAllDocuments ()
    {
        return GoldenDocuments.Value;
    }

    public static JsonDocument ReadJsonDocument (
        string commandName,
        string fileName)
    {
        var repositoryRelativePath = GetPath(commandName, fileName);
        return JsonDocument.Parse(File.ReadAllText(TestRepositoryPaths.GetFullPath(repositoryRelativePath)));
    }

    public static JsonNode ReadJsonNode (
        string commandName,
        string fileName)
    {
        var repositoryRelativePath = GetPath(commandName, fileName);
        return JsonNode.Parse(File.ReadAllText(TestRepositoryPaths.GetFullPath(repositoryRelativePath)))
            ?? throw new InvalidOperationException($"CLI output golden JSON could not be parsed: {repositoryRelativePath}");
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
            .NormalizeGuidProperty("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");
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

    private static string[] EnumerateFullPathsCore ()
    {
        var rootPath = TestRepositoryPaths.GetFullPath(Root);
        return Directory
            .EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] EnumerateRepositoryRelativePathsCore ()
    {
        return FullPaths.Value
            .Select(TestRepositoryPaths.NormalizeRepositoryRelativePath)
            .ToArray();
    }

    private static GoldenDocument[] ReadGoldenDocuments ()
    {
        return RepositoryRelativePaths.Value
            .Select(ReadGoldenDocument)
            .ToArray();
    }

    private static GoldenDocument ReadGoldenDocument (string repositoryRelativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(TestRepositoryPaths.GetFullPath(repositoryRelativePath)));
        return new GoldenDocument(repositoryRelativePath, document.RootElement.Clone());
    }

    internal readonly record struct GoldenDocument (
        string RepositoryRelativePath,
        JsonElement Root);
}
