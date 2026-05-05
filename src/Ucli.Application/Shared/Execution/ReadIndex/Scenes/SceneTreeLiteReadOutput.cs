using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents the successful output of one scene-tree-lite read. </summary>
internal sealed record SceneTreeLiteReadOutput (
    string ScenePath,
    IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> Roots,
    SceneTreeLiteAccessInfo AccessInfo);
