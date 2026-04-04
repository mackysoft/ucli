using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Writes canonical JSON payload bytes used for request-digest material. </summary>
    internal static class CanonicalRequestWriter
    {
        /// <summary> Compares JSON properties by ordinal property-name order for canonical output. </summary>
        private static readonly IComparer<JsonProperty> JsonPropertyNameComparer = Comparer<JsonProperty>.Create(
            static (x, y) => StringComparer.Ordinal.Compare(x.Name, y.Name));

        /// <summary> Writes canonical UTF-8 payload bytes for request digest material. </summary>
        /// <param name="protocolVersion"> The request protocol version. </param>
        /// <param name="steps"> The validated public step sequence. </param>
        /// <returns> Canonical UTF-8 payload bytes containing only <c>protocolVersion</c> and <c>steps</c>. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="steps" /> is <see langword="null" />. </exception>
        public static ReadOnlyMemory<byte> WriteDigestPayload (
            int protocolVersion,
            IReadOnlyList<IpcRequestContractStep> steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = false,
            });

            writer.WriteStartObject();
            writer.WritePropertyName("steps");
            writer.WriteStartArray();
            for (var i = 0; i < steps.Count; i++)
            {
                WriteStep(writer, steps[i]);
            }

            writer.WriteEndArray();
            writer.WriteNumber("protocolVersion", protocolVersion);
            writer.WriteEndObject();
            writer.Flush();
            return stream.ToArray();
        }

        /// <summary> Writes one canonical public step object. </summary>
        /// <param name="writer"> The target writer. </param>
        /// <param name="step"> The public step model. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="step" /> is <see langword="null" />. </exception>
        private static void WriteStep (
            Utf8JsonWriter writer,
            IpcRequestContractStep step)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            WriteCanonicalJsonValueCore(writer, step.Element);
        }

        /// <summary> Writes one canonical JSON value recursively. </summary>
        /// <param name="writer"> The target writer. </param>
        /// <param name="value"> The JSON value to canonicalize. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="writer" /> is <see langword="null" />. </exception>
        internal static void WriteCanonicalJsonValue (
            Utf8JsonWriter writer,
            JsonElement value)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            WriteCanonicalJsonValueCore(writer, value);
        }

        /// <summary> Writes one canonical JSON value recursively. </summary>
        /// <param name="writer"> The target writer. </param>
        /// <param name="value"> The JSON value to canonicalize. </param>
        private static void WriteCanonicalJsonValueCore (
            Utf8JsonWriter writer,
            JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    WriteCanonicalObject(writer, value);
                    return;

                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var arrayItem in value.EnumerateArray())
                    {
                        WriteCanonicalJsonValueCore(writer, arrayItem);
                    }

                    writer.WriteEndArray();
                    return;

                default:
                    value.WriteTo(writer);
                    return;
            }
        }

        /// <summary> Writes one object with properties sorted by ordinal key order. </summary>
        /// <param name="writer"> The target writer. </param>
        /// <param name="value"> The object JSON value. </param>
        private static void WriteCanonicalObject (
            Utf8JsonWriter writer,
            JsonElement value)
        {
            var propertyCount = CountObjectProperties(value);
            if (propertyCount == 0)
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
                return;
            }

            var properties = ArrayPool<JsonProperty>.Shared.Rent(propertyCount);
            try
            {
                var index = 0;
                foreach (var property in value.EnumerateObject())
                {
                    properties[index] = property;
                    index++;
                }

                Array.Sort(properties, 0, propertyCount, JsonPropertyNameComparer);

                writer.WriteStartObject();
                for (var i = 0; i < propertyCount; i++)
                {
                    var property = properties[i];
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJsonValueCore(writer, property.Value);
                }

                writer.WriteEndObject();
            }
            finally
            {
                ArrayPool<JsonProperty>.Shared.Return(properties, clearArray: false);
            }
        }

        /// <summary> Counts object properties without allocating intermediate buffers. </summary>
        /// <param name="value"> The object JSON value. </param>
        /// <returns> The property count. </returns>
        private static int CountObjectProperties (JsonElement value)
        {
            var count = 0;
            foreach (var _ in value.EnumerateObject())
            {
                count++;
            }

            return count;
        }
    }
}
