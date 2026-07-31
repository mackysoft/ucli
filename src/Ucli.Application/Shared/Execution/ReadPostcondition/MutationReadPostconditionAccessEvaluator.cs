using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Applies persisted mutation read-postcondition requirements to candidate read-index timestamps. </summary>
internal static class MutationReadPostconditionAccessEvaluator
{
    public static ValueTask<MutationReadPostconditionEvaluationResult> EvaluateAssetSearchAsync (
        IMutationReadPostconditionStore store,
        ResolvedUnityProjectContext project,
        DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        return EvaluateCoreAsync(
            store,
            project,
            ExecutionReadPostconditionSurface.AssetSearch,
            scenePath: null,
            generatedAtUtc,
            "asset-search",
            cancellationToken);
    }

    public static ValueTask<MutationReadPostconditionEvaluationResult> EvaluateGuidPathAsync (
        IMutationReadPostconditionStore store,
        ResolvedUnityProjectContext project,
        DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        return EvaluateCoreAsync(
            store,
            project,
            ExecutionReadPostconditionSurface.GuidPath,
            scenePath: null,
            generatedAtUtc,
            "guid-path",
            cancellationToken);
    }

    public static ValueTask<MutationReadPostconditionEvaluationResult> EvaluateSceneTreeLiteAsync (
        IMutationReadPostconditionStore store,
        ResolvedUnityProjectContext project,
        UnityScenePath scenePath,
        DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenePath);
        return EvaluateCoreAsync(
            store,
            project,
            ExecutionReadPostconditionSurface.SceneTreeLite,
            scenePath,
            generatedAtUtc,
            "scene-tree-lite",
            cancellationToken);
    }

    private static async ValueTask<MutationReadPostconditionEvaluationResult> EvaluateCoreAsync (
        IMutationReadPostconditionStore store,
        ResolvedUnityProjectContext project,
        ExecutionReadPostconditionSurface surface,
        UnityScenePath? scenePath,
        DateTimeOffset generatedAtUtc,
        string surfaceDescription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceDescription);
        cancellationToken.ThrowIfCancellationRequested();

        var readResult = await store.ReadOrNullAsync(
                project.RepositoryRoot,
                project.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return MutationReadPostconditionEvaluationResult.Reject(readResult.Error!.Message);
        }

        var requirement = FindRequirement(readResult.ReadPostcondition, surface, scenePath);
        if (requirement == null)
        {
            return MutationReadPostconditionEvaluationResult.Allow();
        }

        if (generatedAtUtc >= requirement.MinSafeGeneratedAtUtc)
        {
            return MutationReadPostconditionEvaluationResult.Allow();
        }

        return MutationReadPostconditionEvaluationResult.Reject(
            $"Existing {surfaceDescription} index generatedAtUtc '{generatedAtUtc:O}' is older than mutation read postcondition '{requirement.MinSafeGeneratedAtUtc:O}'.");
    }

    private static ExecutionReadPostconditionRequirement? FindRequirement (
        ExecutionReadPostcondition? readPostcondition,
        ExecutionReadPostconditionSurface surface,
        UnityScenePath? scenePath)
    {
        if (readPostcondition == null)
        {
            return null;
        }

        for (var i = 0; i < readPostcondition.Requirements.Count; i++)
        {
            var requirement = readPostcondition.Requirements[i];
            if (requirement.Surface != surface)
            {
                continue;
            }

            if (!MatchesScenePath(surface, requirement.ScenePath, scenePath))
            {
                continue;
            }

            return requirement;
        }

        return null;
    }

    private static bool MatchesScenePath (
        ExecutionReadPostconditionSurface surface,
        UnityScenePath? requirementScenePath,
        UnityScenePath? scenePath)
    {
        return requirementScenePath == scenePath
            || (surface == ExecutionReadPostconditionSurface.SceneTreeLite
                && requirementScenePath == null);
    }
}
