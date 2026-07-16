using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Scenes;

internal static class SceneTreeLiteSourceSnapshotTestFactory
{
    private const string RootGlobalObjectId = "GlobalObjectId_V1-2-11111111111111111111111111111111-1-0";

    public static SceneTreeLiteSourceSnapshot Create (
        string scenePath,
        string rootName,
        SceneTreeSourceState? sourceState = null)
    {
        return new SceneTreeLiteSourceSnapshot(
            DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
            new UnityScenePath(scenePath),
            [
                new SceneTreeLiteNode(
                    rootName,
                    new UnityGlobalObjectId(RootGlobalObjectId),
                    [],
                    IndexSceneTreeLiteNodeChildrenState.Complete),
            ],
            sourceState ?? new SceneTreeSourceState(SceneTreeSourceStateKind.PersistedPreview, isDirty: false));
    }
}
