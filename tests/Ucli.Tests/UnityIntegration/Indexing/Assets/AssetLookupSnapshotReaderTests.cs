using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Tests.Assets;

public sealed class AssetLookupSnapshotReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ReturnsSnapshot_WhenResponsePayloadIsValid ()
    {
        var executor = new StubUnityIpcRequestExecutor
        {
            Result = UnityIpcRequestExecutionResult.Success(CreateSuccessResponse(new IpcIndexAssetsReadResponse(
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
                ]))),
        };
        var reader = new AssetLookupSnapshotReader(executor);

        var result = await reader.Read(CreateProjectContext().UnityProject, UcliConfig.CreateDefault(), UcliCommandIds.Query, UnityExecutionMode.Auto, TimeSpan.FromMilliseconds(1000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(IpcMethodNames.IndexAssetsRead, executor.LastMethod);
        Assert.Equal(UcliCommandIds.Query, executor.LastCommand);
        Assert.Single(result.Response!.AssetSearchEntries!);
        Assert.Single(result.Response.GuidPathEntries!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ReturnsFailure_WhenResponsePayloadHasMismatchedLookupSets ()
    {
        var executor = new StubUnityIpcRequestExecutor
        {
            Result = UnityIpcRequestExecutionResult.Success(CreateSuccessResponse(new IpcIndexAssetsReadResponse(
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
                ]))),
        };
        var reader = new AssetLookupSnapshotReader(executor);

        var result = await reader.Read(CreateProjectContext().UnityProject, UcliConfig.CreateDefault(), UcliCommandIds.Query, UnityExecutionMode.Auto, TimeSpan.FromMilliseconds(1000));

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("guidPathEntries must be represented in assetSearchEntries.", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_ReturnsSnapshot_WhenAssetSearchContainsEmptyGuidEntries ()
    {
        var executor = new StubUnityIpcRequestExecutor
        {
            Result = UnityIpcRequestExecutionResult.Success(CreateSuccessResponse(new IpcIndexAssetsReadResponse(
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
                ]))),
        };
        var reader = new AssetLookupSnapshotReader(executor);

        var result = await reader.Read(CreateProjectContext().UnityProject, UcliConfig.CreateDefault(), UcliCommandIds.Query, UnityExecutionMode.Auto, TimeSpan.FromMilliseconds(1000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal(2, result.Response!.AssetSearchEntries!.Count);
        Assert.Single(result.Response.GuidPathEntries!);
    }

    private static ProjectContext CreateProjectContext ()
    {
        return new ProjectContext(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/repo/UnityProject",
                RepositoryRoot: "/repo",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static IpcResponse CreateSuccessResponse<TPayload> (TPayload payload)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-1",
            Status: IpcProtocol.StatusOk,
            Payload: JsonSerializer.SerializeToElement(payload),
            Errors: Array.Empty<IpcError>());
    }

    private sealed class StubUnityIpcRequestExecutor : IUnityIpcRequestExecutor
    {
        public UcliCommand LastCommand { get; private set; }

        public string? LastMethod { get; private set; }

        public UnityIpcRequestExecutionResult Result { get; set; }
            = UnityIpcRequestExecutionResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<UnityIpcRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            string method,
            JsonElement payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCommand = command;
            LastMethod = method;
            return ValueTask.FromResult(Result);
        }
    }
}