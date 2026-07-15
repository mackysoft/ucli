using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one serialized-property assignment payload. </summary>
    internal readonly struct SerializedPropertyAssignment
    {
        public SerializedPropertyAssignment (
            SerializedPropertyPath path,
            JsonElement value)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            if (value.ValueKind == JsonValueKind.Undefined)
            {
                throw new ArgumentException("Serialized property value must be defined.", nameof(value));
            }

            Value = value.Clone();
        }

        public SerializedPropertyPath Path { get; }

        public JsonElement Value { get; }
    }
}
