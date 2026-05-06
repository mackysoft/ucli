using System.Text.Json;

namespace MackySoft.Ucli.Architecture.Tests.Architecture;

internal static class UnityAsmdefReferenceReader
{
    internal static string[] Read (string asmdefPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ArchitectureTestRepository.ToRegularFileFullPath(asmdefPath)));
        if (!document.RootElement.TryGetProperty("references", out var references)
            || references.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return references
            .EnumerateArray()
            .Where(static element => element.ValueKind == JsonValueKind.String)
            .Select(static element => element.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();
    }
}
