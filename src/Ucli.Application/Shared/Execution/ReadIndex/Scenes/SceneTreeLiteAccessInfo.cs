namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents internal access metadata produced by scene-tree-lite reads. </summary>
internal sealed record SceneTreeLiteAccessInfo (
    bool Used,
    bool Hit,
    SceneTreeLiteSource Source,
    IndexFreshness Freshness,
    DateTimeOffset? GeneratedAtUtc,
    string? FallbackReason);
