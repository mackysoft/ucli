using System.Text.Json;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Parses static delivery JSON while rejecting duplicate object properties. </summary>
internal static class UniqueJsonDocumentParser
{
    public static JsonDocument Parse (
        byte[] utf8,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(utf8);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{displayName} is not valid JSON.", exception);
        }

        try
        {
            EnsureUniqueProperties(document.RootElement, "$", displayName);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void EnsureUniqueProperties (
        JsonElement element,
        string path,
        string displayName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"{displayName} contains duplicate property '{property.Name}' at '{path}'.");
                }

                EnsureUniqueProperties(property.Value, path + "." + property.Name, displayName);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            EnsureUniqueProperties(item, $"{path}[{index}]", displayName);
            index++;
        }
    }
}
