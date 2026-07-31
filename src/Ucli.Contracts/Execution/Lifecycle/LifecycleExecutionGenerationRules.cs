using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Evaluates the complete Editor-generation ordering shared by Lifecycle Execution boundaries.
/// </summary>
internal static class LifecycleExecutionGenerationRules
{
    internal static bool IsMonotonicSuccessor (
        UnityEditorGenerationSnapshot started,
        UnityEditorGenerationSnapshot candidate)
    {
        if (started is null)
        {
            throw new ArgumentNullException(nameof(started));
        }
        if (candidate is null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        return candidate.CompileGeneration >= started.CompileGeneration
            && candidate.DomainReloadGeneration >= started.DomainReloadGeneration
            && candidate.AssetRefreshGeneration >= started.AssetRefreshGeneration
            && candidate.PlayModeGeneration >= started.PlayModeGeneration;
    }
}
