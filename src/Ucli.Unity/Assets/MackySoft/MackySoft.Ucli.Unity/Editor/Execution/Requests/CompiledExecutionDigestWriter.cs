using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Writes canonical JSON payload bytes used for compiled-execution digest material. </summary>
    internal static class CompiledExecutionDigestWriter
    {
        /// <summary>
        /// Gets the runtime compiler semantics version embedded into compiled-execution digest payloads.
        /// </summary>
        public const int RuntimeCompilerVersion = 1;

        /// <summary>
        /// Gets the selector-resolution semantics version embedded into compiled-execution digest payloads.
        /// </summary>
        public const int SelectorSemanticsVersion = 1;

        /// <summary>
        /// Gets the scene-query semantics version embedded into compiled-execution digest payloads.
        /// </summary>
        public const int QuerySemanticsVersion = 1;

        /// <summary>
        /// Writes one canonical UTF-8 payload used to derive the compiled-execution digest.
        /// </summary>
        /// <param name="steps"> The normalized public steps in source order. Must not be <see langword="null" />. </param>
        /// <param name="operations"> The compiled primitive operations in execution order. Must not be <see langword="null" />. </param>
        /// <returns> The canonical UTF-8 payload that includes compiler versions, normalized steps, and compiled primitive operations. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="steps" /> or <paramref name="operations" /> is <see langword="null" />. </exception>
        public static ReadOnlyMemory<byte> WriteDigestPayload (
            IReadOnlyList<NormalizedRequestStep> steps,
            IReadOnlyList<NormalizedOperation> operations)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

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
            writer.WriteNumber("querySemanticsVersion", QuerySemanticsVersion);
            writer.WriteNumber("runtimeCompilerVersion", RuntimeCompilerVersion);
            writer.WriteNumber("selectorSemanticsVersion", SelectorSemanticsVersion);

            writer.WritePropertyName("steps");
            writer.WriteStartArray();
            for (var i = 0; i < steps.Count; i++)
            {
                WriteStep(writer, steps[i]);
            }

            writer.WriteEndArray();

            writer.WritePropertyName("ops");
            writer.WriteStartArray();
            for (var i = 0; i < operations.Count; i++)
            {
                WriteOperation(writer, operations[i]);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteStep (
            Utf8JsonWriter writer,
            NormalizedRequestStep step)
        {
            writer.WriteStartObject();
            writer.WriteString("id", step.Id);
            writer.WriteString("kind", step.Kind == IpcRequestStepKind.Op ? "op" : "edit");
            writer.WriteString("op", step.OperationName);
            writer.WriteNumber("primitiveCount", step.PrimitiveCount);
            writer.WriteEndObject();
        }

        private static void WriteOperation (
            Utf8JsonWriter writer,
            NormalizedOperation operation)
        {
            writer.WriteStartObject();
            writer.WriteString("id", operation.Id);
            if (!string.IsNullOrWhiteSpace(operation.InternalExecutionKey)
                && !string.Equals(operation.InternalExecutionKey, operation.Id, StringComparison.Ordinal))
            {
                writer.WriteString("internalExecutionKey", operation.InternalExecutionKey);
            }

            writer.WriteString("op", operation.Op);
            if (!string.IsNullOrWhiteSpace(operation.As))
            {
                writer.WriteString("as", operation.As);
            }

            writer.WritePropertyName("args");
            CanonicalRequestWriter.WriteCanonicalJsonValue(writer, operation.Args);
            writer.WriteEndObject();
        }
    }
}
