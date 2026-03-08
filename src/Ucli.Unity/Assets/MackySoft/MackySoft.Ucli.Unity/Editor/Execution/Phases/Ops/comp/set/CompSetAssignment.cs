using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one property assignment in <c>ucli.comp.set</c>. </summary>
    internal readonly struct CompSetAssignment
    {
        public CompSetAssignment (
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