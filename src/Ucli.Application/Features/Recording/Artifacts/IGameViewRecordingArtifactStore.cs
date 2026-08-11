using MackySoft.Ucli.Application.Features.Recording.Registry;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Creates recording-scoped ownership of provider work and immutable artifacts. </summary>
internal interface IGameViewRecordingArtifactStore
{
    /// <summary> Prepares a new recording-scoped artifact lease. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="recordingId"> The non-empty recording identifier. </param>
    /// <param name="admissionLease"> The project admission lease held for this recording identifier. </param>
    /// <returns> The prepared lease or a structured storage error. </returns>
    GameViewRecordingArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        Guid recordingId,
        IGameViewRecordingAdmissionLease admissionLease);

    /// <summary> Opens an existing recording-scoped artifact lease without creating storage. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="recordingId"> The non-empty recording identifier. </param>
    /// <returns> The opened lease or a structured storage error. </returns>
    GameViewRecordingArtifactOpenResult Open (
        ResolvedUnityProjectContext unityProject,
        Guid recordingId);
}
