using System.Text.Json;

namespace MackySoft.Ucli.Tests;

internal static class CliOutputGoldenContractTestSupport
{
    public static void AssertGolden (
        CliOutputGoldenFiles.GoldenDocument golden,
        Action<JsonElement> assert)
    {
        try
        {
            assert(golden.Root);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"CLI output golden contract failed for {golden.RepositoryRelativePath}.", ex);
        }
    }

    public static JsonElement ReadRequiredObject (
        JsonElement owner,
        string propertyName,
        string path)
    {
        Assert.True(owner.TryGetProperty(propertyName, out var property), $"{path} is missing.");
        Assert.Equal(JsonValueKind.Object, property.ValueKind);
        return property;
    }

    public static string ReadRequiredString (
        JsonElement owner,
        string propertyName,
        string path)
    {
        Assert.True(owner.TryGetProperty(propertyName, out var property), $"{path} is missing.");
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        return property.GetString() ?? string.Empty;
    }

    public static void AssertPropertyKind (
        JsonElement owner,
        string propertyName,
        JsonValueKind expectedKind,
        string path)
    {
        Assert.True(owner.TryGetProperty(propertyName, out var property), $"{path} is missing.");
        Assert.Equal(expectedKind, property.ValueKind);
    }

    public static void AssertBooleanProperty (
        JsonElement owner,
        string propertyName,
        string path)
    {
        Assert.True(owner.TryGetProperty(propertyName, out var property), $"{path} is missing.");
        Assert.True(property.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{path} must be boolean.");
    }

    public static bool TryGetProperty (
        JsonElement owner,
        string propertyName,
        JsonValueKind expectedKind,
        out JsonElement property)
    {
        if (owner.ValueKind == JsonValueKind.Object
            && owner.TryGetProperty(propertyName, out property)
            && property.ValueKind == expectedKind)
        {
            return true;
        }

        property = default;
        return false;
    }

    public static bool TryGetString (
        JsonElement owner,
        string propertyName,
        out string value)
    {
        if (owner.ValueKind == JsonValueKind.Object
            && owner.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
