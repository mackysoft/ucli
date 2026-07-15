using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class ReadPostconditionTestFactory
{
    public const string SceneTreeLiteScenePath = "Assets/Scenes/Main.unity";

    public static readonly DateTimeOffset DefaultMinSafeGeneratedAtUtc =
        DateTimeOffset.Parse("2026-04-23T01:02:03+00:00");

    public static OperationExecutionReadPostcondition CreateAssetSearch (
        DateTimeOffset? minSafeGeneratedAtUtc = null)
    {
        return Create(
            IpcExecuteReadPostconditionSurface.AssetSearch,
            minSafeGeneratedAtUtc);
    }

    public static OperationExecutionReadPostcondition CreateSceneTreeLite (
        DateTimeOffset? minSafeGeneratedAtUtc = null,
        string scenePath = SceneTreeLiteScenePath)
    {
        return Create(
            IpcExecuteReadPostconditionSurface.SceneTreeLite,
            minSafeGeneratedAtUtc,
            scenePath);
    }

    public static IpcExecuteReadPostcondition ToIpcContract (OperationExecutionReadPostcondition readPostcondition)
    {
        ArgumentNullException.ThrowIfNull(readPostcondition);

        return new IpcExecuteReadPostcondition(readPostcondition.Requirements.Select(static requirement =>
            new IpcExecuteReadPostconditionRequirement(requirement.Surface, requirement.MinSafeGeneratedAtUtc)
            {
                ScenePath = requirement.ScenePath,
            }).ToArray());
    }

    private static OperationExecutionReadPostcondition Create (
        IpcExecuteReadPostconditionSurface surface,
        DateTimeOffset? minSafeGeneratedAtUtc,
        string? scenePath = null)
    {
        return new OperationExecutionReadPostcondition(
        [
            new OperationExecutionReadPostconditionRequirement(
                Surface: surface,
                MinSafeGeneratedAtUtc: minSafeGeneratedAtUtc ?? DefaultMinSafeGeneratedAtUtc)
            {
                ScenePath = scenePath,
            },
        ]);
    }
}
