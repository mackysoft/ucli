using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one serialized-property assignment payload. </summary>
    internal readonly struct SerializedPropertyAssignment
    {
        public SerializedPropertyAssignment (
            string path,
            JsonElement value)
        {
            Path = path;
            Value = value.Clone();
        }

        public string Path { get; }

        public JsonElement Value { get; }
    }
}