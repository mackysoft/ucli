using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class ReadPostconditionTestFactory
{
    public const string SceneTreeLiteScenePath = "Assets/Scenes/Main.unity";

    public static readonly DateTimeOffset DefaultMinSafeGeneratedAtUtc =
        DateTimeOffset.Parse("2026-04-23T01:02:03+00:00");

    public static IpcExecuteReadPostcondition CreateAssetSearch (
        DateTimeOffset? minSafeGeneratedAtUtc = null)
    {
        return Create(
            IpcExecuteReadPostconditionSurface.AssetSearch,
            minSafeGeneratedAtUtc);
    }

    public static IpcExecuteReadPostcondition CreateSceneTreeLite (
        DateTimeOffset? minSafeGeneratedAtUtc = null,
        string scenePath = SceneTreeLiteScenePath)
    {
        return Create(
            IpcExecuteReadPostconditionSurface.SceneTreeLite,
            minSafeGeneratedAtUtc,
            scenePath);
    }

    private static IpcExecuteReadPostcondition Create (
        IpcExecuteReadPostconditionSurface surface,
        DateTimeOffset? minSafeGeneratedAtUtc,
        string? scenePath = null)
    {
        return new IpcExecuteReadPostcondition(
        [
            new IpcExecuteReadPostconditionRequirement(
                Surface: surface,
                MinSafeGeneratedAtUtc: minSafeGeneratedAtUtc ?? DefaultMinSafeGeneratedAtUtc,
                ScenePath: scenePath == null ? null : new UnityScenePath(scenePath)),
        ]);
    }
}
