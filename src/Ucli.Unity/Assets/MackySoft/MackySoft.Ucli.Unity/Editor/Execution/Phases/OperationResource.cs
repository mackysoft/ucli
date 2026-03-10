#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one editable persistence-unit owner for temporary or live Unity objects. </summary>
    internal readonly struct OperationResource
    {
        public OperationResource (
            OperationTouchKind kind,
            string path)
        {
            Kind = kind;
            Path = path;
        }

        public OperationTouchKind Kind { get; }

        public string Path { get; }
    }
}