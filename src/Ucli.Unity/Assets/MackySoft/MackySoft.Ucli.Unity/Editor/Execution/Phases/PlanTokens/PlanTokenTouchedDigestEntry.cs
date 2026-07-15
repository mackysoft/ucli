using System;
using MackySoft.Ucli.Contracts;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one touched-digest entry. </summary>
    /// <param name="Kind"> The touched-resource kind. </param>
    /// <param name="Path"> The touched project-relative path. </param>
    /// <param name="AssetGuid"> The optional touched asset GUID. </param>
    /// <param name="Exists"> Whether touched path exists at observation time. </param>
    /// <param name="Size"> The touched file size, or <c>-1</c> when unavailable. </param>
    /// <param name="LastWriteUtcTicks"> The last-write timestamp ticks in UTC, or <c>0</c> when unavailable. </param>
    internal sealed record PlanTokenTouchedDigestEntry (
        UcliTouchedResourceKind Kind,
        string Path,
        Guid? AssetGuid,
        bool Exists,
        long Size,
        long LastWriteUtcTicks);
}
