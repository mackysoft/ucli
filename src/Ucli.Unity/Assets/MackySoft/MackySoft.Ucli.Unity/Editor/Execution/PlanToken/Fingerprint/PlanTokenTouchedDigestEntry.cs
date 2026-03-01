#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one touched-digest entry. </summary>
    /// <param name="Kind"> The touched kind literal. </param>
    /// <param name="Path"> The touched project-relative path. </param>
    /// <param name="Guid"> The touched guid value or <c>na</c>. </param>
    /// <param name="Exists"> Whether touched path exists at observation time. </param>
    /// <param name="Size"> The touched file size, or <c>-1</c> when unavailable. </param>
    /// <param name="LastWriteUtcTicks"> The last-write timestamp ticks in UTC, or <c>0</c> when unavailable. </param>
    internal sealed record PlanTokenTouchedDigestEntry (
        string Kind,
        string Path,
        string Guid,
        bool Exists,
        long Size,
        long LastWriteUtcTicks);
}
