using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;

namespace MackySoft.Ucli.Tests.Assets;

public sealed class AssetLookupSnapshotReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ReturnsSnapshot_WhenResponsePayloadIsValid ()
    {
        var executor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateSuccessResponse(new IpcIndexAssetsReadResponse(
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
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
                ]))));
        var reader = new AssetLookupSnapshotReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1000),
            failFast: true);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        var execution = UnityRequestExecutorAssert.PayloadExecutedOnce<UnityRequestPayload.IndexAssetsRead>(
            executor,
            UcliCommandIds.Query,
            UnityExecutionMode.Auto);
        Assert.True(execution.Payload.FailFast);
        var assetSearchEntry = Assert.Single(result.Snapshot!.AssetSearchEntries);
        Assert.Equal(Guid.ParseExact("11111111111111111111111111111111", "N"), assetSearchEntry.AssetGuid);
        Assert.Single(result.Snapshot.GuidPathEntries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ReturnsFailure_WhenResponsePayloadHasMismatchedLookupSets ()
    {
        var executor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateSuccessResponse(new IpcIndexAssetsReadResponse(
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
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
                            "UnityEngine.Object, UnityEngine.CoreModule",
                        ]),
                ],
                GuidPathEntries:
                [
                    new IndexGuidPathEntryJsonContract(
                        AssetGuid: "22222222222222222222222222222222",
                        AssetPath: "Assets/Data/Spawner.asset"),
                ]))));
        var reader = new AssetLookupSnapshotReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1000));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("guidPathEntries must be represented in assetSearchEntries.", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ReturnsSnapshot_WhenAssetSearchContainsEmptyGuidEntries ()
    {
        var executor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(CreateSuccessResponse(new IpcIndexAssetsReadResponse(
                GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
                AssetSearchEntries:
                [
                    new IndexAssetSearchEntryJsonContract(
                        AssetPath: "Assets/Data/NoGuid.asset",
                        AssetGuid: string.Empty,
                        Name: "NoGuid",
                        TypeId: "Game.NoGuid, Assembly-CSharp",
                        SearchTypeIds:
                        [
                            "Game.NoGuid, Assembly-CSharp",
                            "UnityEngine.Object, UnityEngine.CoreModule",
                        ]),
                    new IndexAssetSearchEntryJsonContract(
                        AssetPath: "Assets/Data/Spawner.asset",
                        AssetGuid: "11111111111111111111111111111111",
                        Name: "Spawner",
                        TypeId: "Game.Spawner, Assembly-CSharp",
                        SearchTypeIds:
                        [
                            "Game.Spawner, Assembly-CSharp",
                            "UnityEngine.Object, UnityEngine.CoreModule",
                        ]),
                ],
                GuidPathEntries:
                [
                    new IndexGuidPathEntryJsonContract(
                        AssetGuid: "11111111111111111111111111111111",
                        AssetPath: "Assets/Data/Spawner.asset"),
                ]))));
        var reader = new AssetLookupSnapshotReader(executor);

        var result = await reader.ReadAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault(),
            UcliCommandIds.Query,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(2, result.Snapshot!.AssetSearchEntries.Count);
        Assert.Null(result.Snapshot.AssetSearchEntries[0].AssetGuid);
        Assert.Single(result.Snapshot.GuidPathEntries);
    }

    private static UnityRequestResponse CreateSuccessResponse<TPayload> (TPayload payload)
    {
        return CreateResponse(IpcResponseStatus.Ok, payload, Array.Empty<IpcError>());
    }

    private static UnityRequestResponse CreateResponse<TPayload> (
        IpcResponseStatus status,
        TPayload payload,
        IReadOnlyList<IpcError> errors)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: status,
            payload: JsonSerializer.SerializeToElement(payload),
            errors: errors));
    }

}
