using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Unity.Execution.Requests;

namespace MackySoft.Ucli.Unity.Execution.Phases
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
            writer.WriteString("id", step.Id.Value);
            writer.WriteString("kind", step.Kind == IpcExecuteStepKind.Op ? "op" : "edit");
            writer.WriteString("op", step.OperationName);
            writer.WriteNumber("primitiveCount", step.PrimitiveCount);
            writer.WriteEndObject();
        }

        private static void WriteOperation (
            Utf8JsonWriter writer,
            NormalizedOperation operation)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("executionKey");
            writer.WriteStartObject();
            writer.WriteString(
                "kind",
                operation.ExecutionKey.IsEditPrimitive ? "editPrimitive" : "rawStep");
            writer.WriteString("stepId", operation.ExecutionKey.StepId.Value);
            if (operation.ExecutionKey.IsEditPrimitive)
            {
                writer.WriteNumber("primitiveIndex", operation.ExecutionKey.PrimitiveIndex);
            }

            writer.WriteEndObject();

            writer.WriteString("op", operation.Op);
            if (operation.As != null)
            {
                writer.WritePropertyName("as");
                if (operation.As is RequestLocalAliasIdentity.EditActionAliasIdentity editActionAlias)
                {
                    WriteEditActionAlias(writer, editActionAlias);
                }
                else
                {
                    writer.WriteStringValue(operation.As.Alias.Value);
                }
            }

            if (operation.AliasReferences.InternalAliasCount > 0)
            {
                writer.WritePropertyName("aliasReferences");
                writer.WriteStartArray();
                for (var i = 0; i < operation.AliasReferences.InternalAliasCount; i++)
                {
                    WriteEditActionAlias(writer, operation.AliasReferences[i]);
                }

                writer.WriteEndArray();
            }

            writer.WritePropertyName("args");
            CanonicalRequestWriter.WriteCanonicalJsonValue(writer, operation.Args);
            writer.WriteEndObject();
        }

        private static void WriteEditActionAlias (
            Utf8JsonWriter writer,
            RequestLocalAliasIdentity.EditActionAliasIdentity alias)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", "editAction");
            writer.WriteString("stepId", alias.StepId.Value);
            writer.WriteNumber("branchIndex", alias.BranchIndex);
            writer.WriteString("alias", alias.Alias.Value);
            writer.WriteEndObject();
        }
    }
}
