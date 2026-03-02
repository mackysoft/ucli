using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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
        /// <param name="operations"> The normalized operation sequence. </param>
        /// <returns> Canonical UTF-8 payload bytes containing only <c>protocolVersion</c> and <c>ops</c>. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operations" /> is <see langword="null" />. </exception>
        public static ReadOnlyMemory<byte> WriteDigestPayload (
            int protocolVersion,
            IReadOnlyList<NormalizedOperation> operations)
        {
            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = false,
            });

            writer.WriteStartObject();
            writer.WritePropertyName("ops");
            writer.WriteStartArray();
            for (var i = 0; i < operations.Count; i++)
            {
                WriteOperation(writer, operations[i]);
            }

            writer.WriteEndArray();
            writer.WriteNumber("protocolVersion", protocolVersion);
            writer.WriteEndObject();
            writer.Flush();
            return stream.ToArray();
        }

        /// <summary> Writes one canonical operation object. </summary>
        /// <param name="writer"> The target writer. </param>
        /// <param name="operation"> The operation model. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operation" /> is <see langword="null" />. </exception>
        private static void WriteOperation (
            Utf8JsonWriter writer,
            NormalizedOperation operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            writer.WriteStartObject();
            writer.WritePropertyName("args");
            WriteCanonicalJsonValueCore(writer, operation.Args);

            if (operation.As is not null)
            {
                writer.WriteString("as", operation.As);
            }

            if (operation.Expect is not null)
            {
                writer.WritePropertyName("expect");
                WriteExpectation(writer, operation.Expect);
            }

            writer.WriteString("id", operation.Id);
            writer.WriteString("op", operation.Op);
            writer.WriteEndObject();
        }

        /// <summary> Writes one canonical expectation object. </summary>
        /// <param name="writer"> The target writer. </param>
        /// <param name="expectation"> The expectation model. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="expectation" /> is <see langword="null" />. </exception>
        private static void WriteExpectation (
            Utf8JsonWriter writer,
            NormalizedExpectation expectation)
        {
            if (expectation == null)
            {
                throw new ArgumentNullException(nameof(expectation));
            }

            writer.WriteStartObject();
            if (expectation.Count.HasValue)
            {
                writer.WriteNumber("count", expectation.Count.Value);
            }

            if (expectation.Max.HasValue)
            {
                writer.WriteNumber("max", expectation.Max.Value);
            }

            if (expectation.Min.HasValue)
            {
                writer.WriteNumber("min", expectation.Min.Value);
            }

            if (expectation.NonNull.HasValue)
            {
                writer.WriteBoolean("nonNull", expectation.NonNull.Value);
            }

            writer.WriteEndObject();
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
