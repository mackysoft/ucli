using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Execution.ReadPostcondition;

/// <summary> Applies persisted mutation read-postcondition requirements to candidate read-index timestamps. </summary>
internal static class MutationReadPostconditionAccessEvaluator
{
    public static ValueTask<MutationReadPostconditionEvaluationResult> EvaluateAssetSearch (
        IMutationReadPostconditionStore store,
        ResolvedUnityProjectContext project,
        DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        return EvaluateCore(
            store,
            project,
            IpcExecuteReadPostconditionSurfaceNames.AssetSearch,
            scenePath: null,
            generatedAtUtc,
            "asset-search",
            cancellationToken);
    }

    public static ValueTask<MutationReadPostconditionEvaluationResult> EvaluateGuidPath (
        IMutationReadPostconditionStore store,
        ResolvedUnityProjectContext project,
        DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        return EvaluateCore(
            store,
            project,
            IpcExecuteReadPostconditionSurfaceNames.GuidPath,
            scenePath: null,
            generatedAtUtc,
            "guid-path",
            cancellationToken);
    }

    public static ValueTask<MutationReadPostconditionEvaluationResult> EvaluateSceneTreeLite (
        IMutationReadPostconditionStore store,
        ResolvedUnityProjectContext project,
        string scenePath,
        DateTimeOffset generatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        return EvaluateCore(
            store,
            project,
            IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
            PathStringNormalizer.ToSlashSeparated(scenePath),
            generatedAtUtc,
            "scene-tree-lite",
            cancellationToken);
    }

    private static async ValueTask<MutationReadPostconditionEvaluationResult> EvaluateCore (
        IMutationReadPostconditionStore store,
        ResolvedUnityProjectContext project,
        string surface,
        string? scenePath,
        DateTimeOffset generatedAtUtc,
        string surfaceDescription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceDescription);
        cancellationToken.ThrowIfCancellationRequested();

        var readResult = await store.ReadOrNull(
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

    private static OperationExecutionReadPostconditionRequirement? FindRequirement (
        OperationExecutionReadPostcondition? readPostcondition,
        string surface,
        string? scenePath)
    {
        if (readPostcondition == null)
        {
            return null;
        }

        for (var i = 0; i < readPostcondition.Requirements.Count; i++)
        {
            var requirement = readPostcondition.Requirements[i];
            if (!string.Equals(requirement.Surface, surface, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(requirement.ScenePath, scenePath, StringComparison.Ordinal))
            {
                continue;
            }

            return requirement;
        }

        return null;
    }
}
