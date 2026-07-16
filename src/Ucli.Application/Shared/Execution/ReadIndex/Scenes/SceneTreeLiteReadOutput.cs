using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents the successful output of one scene-tree-lite read. </summary>
internal sealed record SceneTreeLiteReadOutput
{
    public SceneTreeLiteReadOutput (
        UnityScenePath ScenePath,
        IReadOnlyList<SceneTreeLiteNode> Roots,
        SceneTreeSourceState SourceState,
        SceneTreeLiteAccessInfo AccessInfo)
    {
        this.ScenePath = ScenePath ?? throw new ArgumentNullException(nameof(ScenePath));
        ArgumentNullException.ThrowIfNull(Roots);
        this.Roots = Array.AsReadOnly(Roots.ToArray());
        this.SourceState = SourceState ?? throw new ArgumentNullException(nameof(SourceState));
        this.AccessInfo = AccessInfo ?? throw new ArgumentNullException(nameof(AccessInfo));
    }

    public UnityScenePath ScenePath { get; }

    public IReadOnlyList<SceneTreeLiteNode> Roots { get; }

    public SceneTreeSourceState SourceState { get; }

    public SceneTreeLiteAccessInfo AccessInfo { get; }
}
