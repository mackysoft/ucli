using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents the successful output of one scene-tree-lite read. </summary>
internal sealed record SceneTreeLiteReadOutput (
    string ScenePath,
    IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots,
    SceneTreeSourceState SourceState,
    SceneTreeLiteAccessInfo AccessInfo);
