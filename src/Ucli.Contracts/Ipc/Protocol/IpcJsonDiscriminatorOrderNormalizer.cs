using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>
/// Moves the applicable shared polymorphic discriminator to the first property of each JSON object
/// before System.Text.Json 8 deserialization, without interpreting discriminator values.
/// </summary>
internal static class IpcJsonDiscriminatorOrderNormalizer
{
    public static bool TryNormalize (
        JsonElement value,
        out byte[] normalizedUtf8)
    {
        if (!RequiresNormalization(value))
        {
            normalizedUtf8 = Array.Empty<byte>();
            return false;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteValue(writer, value);
        }

        normalizedUtf8 = stream.ToArray();
        return true;
    }

    private static bool RequiresNormalization (JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var discriminatorPropertyName =
                    GetDiscriminatorPropertyName(value);
                var propertyIndex = 0;
                foreach (var property in value.EnumerateObject())
                {
                    if (discriminatorPropertyName is not null
                        && property.NameEquals(discriminatorPropertyName)
                        && propertyIndex != 0)
                    {
                        return true;
                    }

                    if (RequiresNormalization(property.Value))
                    {
                        return true;
                    }

                    propertyIndex++;
                }

                return false;

            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (RequiresNormalization(item))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    private static void WriteValue (
        Utf8JsonWriter writer,
        JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(writer, value);
                return;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                return;

            default:
                value.WriteTo(writer);
                return;
        }
    }

    private static void WriteObject (
        Utf8JsonWriter writer,
        JsonElement value)
    {
        var discriminatorPropertyName = GetDiscriminatorPropertyName(value);
        writer.WriteStartObject();
        if (discriminatorPropertyName is not null)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (property.NameEquals(discriminatorPropertyName))
                {
                    WriteProperty(writer, property);
                    break;
                }
            }
        }

        foreach (var property in value.EnumerateObject())
        {
            if (discriminatorPropertyName is null
                || !property.NameEquals(discriminatorPropertyName))
            {
                WriteProperty(writer, property);
            }
        }

        writer.WriteEndObject();
    }

    private static string? GetDiscriminatorPropertyName (JsonElement value)
    {
        // A reference also carries its product-defined "kind", so the tagged-union discriminator
        // with the more specific structural role must win over the nested value vocabulary.
        if (value.TryGetProperty("lifecycle", out _))
        {
            return "lifecycle";
        }

        if (value.TryGetProperty("locationKind", out _))
        {
            return "locationKind";
        }

        if (value.TryGetProperty("executionKind", out _))
        {
            return "executionKind";
        }

        return value.TryGetProperty(
            UcliOperationContractPropertyNames.Kind,
            out _)
            ? UcliOperationContractPropertyNames.Kind
            : null;
    }

    private static void WriteProperty (
        Utf8JsonWriter writer,
        JsonProperty property)
    {
        writer.WritePropertyName(property.Name);
        WriteValue(writer, property.Value);
    }
}
