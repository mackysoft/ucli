using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcIndexReadContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcIndexAssetsReadContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcIndexAssetsReadRequest(FailFast: true);
        var responsePayload = new IpcIndexAssetsReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            AssetSearchEntries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Data/Spawner.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "Spawner",
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "Game.Spawner, Assembly-CSharp",
                        "UnityEngine.ScriptableObject, UnityEngine.CoreModule",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ],
            GuidPathEntries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "11111111111111111111111111111111",
                    AssetPath: "Assets/Data/Spawner.asset"),
            ]);

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasBoolean("failFast", true);
        JsonAssert.For(response)
            .HasString("generatedAtUtc", "2026-03-06T00:00:00+00:00")
            .HasArrayLength("assetSearchEntries", 1)
            .HasArrayLength("guidPathEntries", 1)
            .HasProperty("assetSearchEntries", 0, entry => entry
                .HasString("assetPath", "Assets/Data/Spawner.asset")
                .HasString("assetGuid", "11111111111111111111111111111111")
                .HasString("name", "Spawner")
                .HasString("typeId", "Game.Spawner, Assembly-CSharp")
                .HasArrayLength("searchTypeIds", 3))
            .HasProperty("guidPathEntries", 0, entry => entry
                .HasString("assetGuid", "11111111111111111111111111111111")
                .HasString("assetPath", "Assets/Data/Spawner.asset"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcIndexSceneTreeLiteReadContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcIndexSceneTreeLiteReadRequest(ScenePath: "Assets/Scenes/Sample.unity");
        var responsePayload = new IpcIndexSceneTreeLiteReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Child",
                            globalObjectId: string.Empty,
                            children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ],
            SourceState: new SceneTreeSourceState(SceneTreeSourceStateKind.LoadedScene, isDirty: true));

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasString("scenePath", "Assets/Scenes/Sample.unity")
            .HasBoolean("failFast", false)
            .HasBoolean("loadedSceneOnly", false);
        JsonAssert.For(response)
            .HasString("generatedAtUtc", "2026-03-06T00:00:00+00:00")
            .HasString("scenePath", "Assets/Scenes/Sample.unity")
            .HasProperty("sourceState", state => state
                .HasString("kind", "loadedScene")
                .HasBoolean("isDirty", true))
            .HasArrayLength("roots", 1)
            .HasProperty("roots", 0, node => node
                .HasString("name", "Root")
                .HasString("globalObjectId", "GlobalObjectId_V1-2-3-4-5-6")
                .HasString("childrenState", IndexSceneTreeLiteNodeChildrenStateValues.Complete)
                .HasArrayLength("children", 1));
    }
}
