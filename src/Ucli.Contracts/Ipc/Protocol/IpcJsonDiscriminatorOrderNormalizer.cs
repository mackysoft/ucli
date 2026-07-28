using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>
/// Moves the shared polymorphic discriminator to the first property of each JSON object before
/// System.Text.Json 8 deserialization, without interpreting object shape or discriminator values.
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
                var propertyIndex = 0;
                foreach (var property in value.EnumerateObject())
                {
                    if (property.NameEquals(UcliOperationContractPropertyNames.Kind) && propertyIndex != 0)
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
        writer.WriteStartObject();
        foreach (var property in value.EnumerateObject())
        {
            if (property.NameEquals(UcliOperationContractPropertyNames.Kind))
            {
                WriteProperty(writer, property);
                break;
            }
        }

        foreach (var property in value.EnumerateObject())
        {
            if (!property.NameEquals(UcliOperationContractPropertyNames.Kind))
            {
                WriteProperty(writer, property);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteProperty (
        Utf8JsonWriter writer,
        JsonProperty property)
    {
        writer.WritePropertyName(property.Name);
        WriteValue(writer, property.Value);
    }
}
