using System.Text.Json;
using System.Text.Json.Nodes;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Schemas;

internal static class StaticSchemaSetTestSupport
{
    private const string ManifestFileName = "schema-manifest.json";

    public static string CopyRepositorySchemaSet (
        TestDirectoryScope scope,
        string relativeDestination = "installed")
    {
        ArgumentNullException.ThrowIfNull(scope);

        var sourceRoot = TestRepositoryPaths.GetFullPath("schemas");
        var destinationRoot = scope.CreateDirectory(relativeDestination);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath);
        }

        return destinationRoot;
    }

    public static JsonObject ReadManifest (string schemaRoot)
    {
        var manifestPath = Path.Combine(schemaRoot, ManifestFileName);
        return JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
    }

    public static void WriteManifest (
        string schemaRoot,
        JsonObject manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var manifestPath = Path.Combine(schemaRoot, ManifestFileName);
        File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }

    public static JsonObject GetFirstEntry (JsonObject manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return manifest["schemas"]!.AsArray()[0]!.AsObject();
    }
}
