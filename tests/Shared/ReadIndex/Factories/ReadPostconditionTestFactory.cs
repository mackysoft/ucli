using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.TestSupport;

internal static class ReadPostconditionTestFactory
{
    public const string SceneTreeLiteScenePath = "Assets/Scenes/Main.unity";

    public static readonly DateTimeOffset DefaultMinSafeGeneratedAtUtc =
        DateTimeOffset.Parse("2026-04-23T01:02:03+00:00");

    public static ExecutionReadPostcondition CreateAssetSearch (
        DateTimeOffset? minSafeGeneratedAtUtc = null)
    {
        return Create(
            ExecutionReadPostconditionSurface.AssetSearch,
            minSafeGeneratedAtUtc);
    }

    public static ExecutionReadPostcondition CreateSceneTreeLite (
        DateTimeOffset? minSafeGeneratedAtUtc = null,
        string scenePath = SceneTreeLiteScenePath)
    {
        return Create(
            ExecutionReadPostconditionSurface.SceneTreeLite,
            minSafeGeneratedAtUtc,
            scenePath);
    }

    private static ExecutionReadPostcondition Create (
        ExecutionReadPostconditionSurface surface,
        DateTimeOffset? minSafeGeneratedAtUtc,
        string? scenePath = null)
    {
        return new ExecutionReadPostcondition(
        [
            new ExecutionReadPostconditionRequirement(
                Surface: surface,
                MinSafeGeneratedAtUtc: minSafeGeneratedAtUtc ?? DefaultMinSafeGeneratedAtUtc,
                ScenePath: scenePath == null ? null : new UnityScenePath(scenePath)),
        ]);
    }
}
