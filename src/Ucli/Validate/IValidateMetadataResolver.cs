using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Validate;

/// <summary> Resolves static-validation operation metadata and emitted read-index payload information. </summary>
internal interface IValidateMetadataResolver
{
    /// <summary> Resolves operation metadata for one validate execution. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="readIndexMode"> The effective read-index mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to metadata and read-index output information. </returns>
    ValueTask<ValidateMetadataResolutionResult> Resolve (
        ResolvedUnityProjectContext unityProject,
        MackySoft.Ucli.Contracts.Configuration.ReadIndexMode readIndexMode,
        CancellationToken cancellationToken = default);
}