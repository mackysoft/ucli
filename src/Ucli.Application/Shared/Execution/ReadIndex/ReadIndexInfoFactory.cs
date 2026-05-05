namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Creates application read-index metadata from access metadata. </summary>
internal static class ReadIndexInfoFactory
{
    /// <summary> Creates readIndex metadata from asset-search lookup access metadata. </summary>
    public static ReadIndexInfo FromAssetLookupAccess (AssetLookupAccessInfo accessInfo)
    {
        ArgumentNullException.ThrowIfNull(accessInfo);

        return new ReadIndexInfo(
            Used: accessInfo.Used && accessInfo.Source == AssetLookupSource.Index,
            Hit: accessInfo.Hit,
            Source: accessInfo.Source == AssetLookupSource.Index
                ? ReadIndexInfoSource.Index
                : ReadIndexInfoSource.Unity,
            Freshness: accessInfo.Freshness,
            GeneratedAtUtc: accessInfo.GeneratedAtUtc,
            FallbackReason: accessInfo.FallbackReason);
    }

    /// <summary> Creates readIndex metadata from scene-tree-lite access metadata. </summary>
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
