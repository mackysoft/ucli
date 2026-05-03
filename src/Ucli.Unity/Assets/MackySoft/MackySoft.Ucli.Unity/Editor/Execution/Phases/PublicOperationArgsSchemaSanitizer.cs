using System;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Rewrites internal operation argument schemas into the public raw-op contract surface. </summary>
    internal static class PublicOperationArgsSchemaSanitizer
    {
        public static string Sanitize (string schemaJson)
        {
            if (string.IsNullOrWhiteSpace(schemaJson))
            {
                throw new ArgumentException("Schema JSON must not be null, empty, or whitespace.", nameof(schemaJson));
            }

            using var document = JsonDocument.Parse(schemaJson);
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteSanitizedElement(document.RootElement, writer, parentPropertyName: null);
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        private static void WriteSanitizedElement (
            JsonElement element,
            Utf8JsonWriter writer,
            string? parentPropertyName)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    WriteSanitizedObject(element, writer);
                    return;

                case JsonValueKind.Array:
                    WriteSanitizedArray(element, writer, parentPropertyName);
                    return;

                default:
                    element.WriteTo(writer);
                    return;
            }
        }

        private static void WriteSanitizedObject (
            JsonElement element,
            Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, "var", StringComparison.Ordinal))
                {
                    continue;
                }

                writer.WritePropertyName(property.Name);
                WriteSanitizedElement(property.Value, writer, property.Name);
            }

            writer.WriteEndObject();
        }

        private static void WriteSanitizedArray (
            JsonElement element,
            Utf8JsonWriter writer,
            string? parentPropertyName)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                if (string.Equals(parentPropertyName, "required", StringComparison.Ordinal)
                    && item.ValueKind == JsonValueKind.String
                    && string.Equals(item.GetString(), "var", StringComparison.Ordinal))
                {
                    continue;
                }

                WriteSanitizedElement(item, writer, parentPropertyName: null);
            }

            writer.WriteEndArray();
        }
    }
}
