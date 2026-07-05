using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Tests.Helpers.Assurance.Build;

internal static class BuildOutputManifestJsonContractTestSupport
{
    public static BuildOutputManifestContentJsonContract ReadContent (JsonElement root)
    {
        var targetElement = root.GetProperty("target");
        var entryElements = root.GetProperty("entries");
        var entries = new List<BuildOutputManifestEntryJsonContract>(entryElements.GetArrayLength());
        foreach (var entry in entryElements.EnumerateArray())
        {
            entries.Add(new BuildOutputManifestEntryJsonContract(
                entry.GetProperty("id").GetString()!,
                entry.GetProperty("kind").GetString()!,
                entry.GetProperty("sourcePath").GetString()!));
        }

        var fileElements = root.GetProperty("files");
        var files = new List<BuildOutputManifestFileJsonContract>(fileElements.GetArrayLength());
        foreach (var file in fileElements.EnumerateArray())
        {
            files.Add(new BuildOutputManifestFileJsonContract(
                file.GetProperty("entryId").GetString()!,
                file.GetProperty("logicalPath").GetString()!,
                file.GetProperty("sourcePath").GetString()!,
                file.GetProperty("artifactPath").GetString()!,
                file.GetProperty("sizeBytes").GetInt64(),
                file.GetProperty("sha256").GetString()!));
        }

        return new BuildOutputManifestContentJsonContract(
            root.GetProperty("schemaVersion").GetInt32(),
            new BuildOutputManifestTargetJsonContract(
                targetElement.GetProperty("stableName").GetString()!,
                targetElement.GetProperty("unityBuildTarget").GetString()!),
            entries,
            root.GetProperty("entryCount").GetInt32(),
            root.GetProperty("fileCount").GetInt32(),
            root.GetProperty("totalBytes").GetInt64(),
            files);
    }
}
