using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

namespace MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Creates command-facing read-index metadata for <c>ucli resolve</c>. </summary>
internal static class ResolveReadIndexInfoFactory
{
    /// <summary> Creates index-backed readIndex metadata from scene-tree-lite access metadata. </summary>
    public static ReadIndexInfo FromSceneTreeLiteAccess (SceneTreeLiteAccessInfo accessInfo)
    {
        ArgumentNullException.ThrowIfNull(accessInfo);

        return new ReadIndexInfo(
            Used: accessInfo.Used && accessInfo.Source == SceneTreeLiteSource.Index,
            Hit: accessInfo.Hit,
            Source: accessInfo.Source == SceneTreeLiteSource.Index
                ? ReadIndexInfoSource.Index
                : ReadIndexInfoSource.Unity,
            Freshness: accessInfo.Freshness,
            GeneratedAtUtc: accessInfo.GeneratedAtUtc,
            FallbackReason: accessInfo.FallbackReason);
    }

    /// <summary> Creates Unity-backed readIndex metadata. </summary>
    public static ReadIndexInfo Unity (string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: false,
            Hit: false,
            Source: ReadIndexInfoSource.Unity,
            Freshness: IndexFreshness.Fresh,
            GeneratedAtUtc: null,
            FallbackReason: fallbackReason);
    }
}
