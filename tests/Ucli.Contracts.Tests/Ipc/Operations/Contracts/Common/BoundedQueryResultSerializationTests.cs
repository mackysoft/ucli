using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class BoundedQueryResultSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void SerializeToElement_WhenBoundedResultsContainWindow_EmitsCursorWindowWithoutLegacyAfterField ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(1);
        var nextCursor = BoundedWindowCursorCodec.Encode(2);
        var assetsResult = new AssetsFindResult(
            matches:
            [
                new AssetsFindMatch(
                    assetPath: "Assets/Data/A.asset",
                    assetGuid: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    name: "A",
                    typeId: "UnityEngine.ScriptableObject, UnityEngine.CoreModule"),
            ],
            window: new BoundedWindow(
                limit: 1,
                cursor: cursor,
                nextCursor: nextCursor,
                isComplete: false,
                totalCount: 3));
        var sceneResult = new SceneTreeResult(
            path: new SceneAssetPath("Assets/Scenes/Main.unity"),
            roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-1-2-3-4-5",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ],
            sourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.ReadIndex, isDirty: false),
            window: new BoundedWindow(
                limit: 1,
                cursor: null,
                nextCursor: nextCursor,
                isComplete: false,
                totalCount: 2));

        var assetsElement = IpcPayloadCodec.SerializeToElement(assetsResult);
        var sceneElement = IpcPayloadCodec.SerializeToElement(sceneResult);

        JsonAssert.For(assetsElement)
            .HasArrayLength("matches", 1)
            .HasProperty("window", window => window
                .HasInt32("limit", 1)
                .HasString("cursor", cursor)
                .HasString("nextCursor", nextCursor)
                .HasBoolean("isComplete", false)
                .HasInt32("totalCount", 3));
        Assert.False(assetsElement.GetProperty("window").TryGetProperty("after", out _));
        JsonAssert.For(sceneElement)
            .HasArrayLength("roots", 1)
            .HasProperty("roots", 0, root => root
                .HasString("childrenState", IndexSceneTreeLiteNodeChildrenStateValues.Complete))
            .HasProperty("window", window => window
                .IsNull("cursor")
                .HasString("nextCursor", nextCursor));
        Assert.False(sceneElement.GetProperty("window").TryGetProperty("after", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SerializeToElement_WhenSceneTreeNodeStateIsUnknown_EmitsUnknownChildrenState ()
    {
        var node = new IndexSceneTreeLiteNodeJsonContract(
            name: "Root",
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5",
            children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Unknown);

        var element = IpcPayloadCodec.SerializeToElement(node);

        JsonAssert.For(element)
            .HasString("childrenState", IndexSceneTreeLiteNodeChildrenStateValues.Unknown);
    }
}
