#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one touched persistence-unit entry from operation execution. </summary>
    /// <param name="Kind"> The touched unit kind. </param>
    /// <param name="Path"> The project-relative path. </param>
    /// <param name="Guid"> The optional asset guid. </param>
    internal sealed record OperationTouch (
        OperationTouchKind Kind,
        string Path,
        string? Guid);
}
