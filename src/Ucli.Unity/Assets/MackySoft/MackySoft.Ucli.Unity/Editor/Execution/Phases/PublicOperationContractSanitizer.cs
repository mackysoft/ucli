using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Rewrites internal-only operation contract details into the public raw-op contract surface. </summary>
    internal static class PublicOperationContractSanitizer
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

        public static IReadOnlyList<UcliOperationInputContract> SanitizeInputs (IReadOnlyList<UcliOperationInputContract> inputs)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            var sanitizedInputs = new UcliOperationInputContract[inputs.Count];
            for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
            {
                var input = inputs[inputIndex] ?? throw new ArgumentException("Input contract entries must not be null.", nameof(inputs));
                sanitizedInputs[inputIndex] = new UcliOperationInputContract(
                    input.Name,
                    input.ValueType,
                    input.Description,
                    input.Constraints,
                    input.ArgsPath,
                    SanitizeVariants(input.Variants));
            }

            return sanitizedInputs;
        }

        private static IReadOnlyList<UcliOperationInputVariantContract>? SanitizeVariants (IReadOnlyList<UcliOperationInputVariantContract>? variants)
        {
            if (variants == null)
            {
                return null;
            }

            var sanitizedVariants = new List<UcliOperationInputVariantContract>(variants.Count);
            for (var variantIndex = 0; variantIndex < variants.Count; variantIndex++)
            {
                var variant = variants[variantIndex] ?? throw new ArgumentException("Input variant entries must not be null.", nameof(variants));
                if (ContainsAliasArgsPath(variant.ArgsPaths))
                {
                    continue;
                }

                sanitizedVariants.Add(new UcliOperationInputVariantContract(
                    variant.Name,
                    variant.Description,
                    variant.ArgsPaths,
                    variant.Constraints));
            }

            return sanitizedVariants.Count == 0 ? null : sanitizedVariants;
        }

        private static bool ContainsAliasArgsPath (IReadOnlyList<string>? argsPaths)
        {
            if (argsPaths == null)
            {
                return false;
            }

            for (var argsPathIndex = 0; argsPathIndex < argsPaths.Count; argsPathIndex++)
            {
                if (ContainsAliasPathSegment(argsPaths[argsPathIndex]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAliasPathSegment (string? argsPath)
        {
            if (string.IsNullOrEmpty(argsPath))
            {
                return false;
            }

            return string.Equals(argsPath, "$." + UcliOperationContractPropertyNames.Alias, StringComparison.Ordinal)
                || argsPath.EndsWith("." + UcliOperationContractPropertyNames.Alias, StringComparison.Ordinal)
                || argsPath.IndexOf("." + UcliOperationContractPropertyNames.Alias + ".", StringComparison.Ordinal) >= 0;
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
