using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents Unity Build Profile input resolution output. </summary>
    internal sealed record UnityBuildProfileInputResolutionResult (
        bool IsSuccess,
        UnityBuildPreconditionInput? PreconditionInput,
        ResolvedBuildPipelineOutputLayout? OutputLayout,
        IpcUnityBuildProfileInput? UnityBuildProfile,
        UnityEditorObservation? LifecycleBefore,
        IpcBuildDirtyState? DirtyState,
        IpcError? Error)
    {
        /// <summary> Creates a successful input resolution result. </summary>
        public static UnityBuildProfileInputResolutionResult Success (
            UnityBuildPreconditionInput preconditionInput,
            ResolvedBuildPipelineOutputLayout outputLayout,
            IpcUnityBuildProfileInput unityBuildProfile)
        {
            return new UnityBuildProfileInputResolutionResult(
                true,
                preconditionInput,
                outputLayout,
                unityBuildProfile,
                unityBuildProfile.ApplyAudit?.LifecycleAfter,
                unityBuildProfile.ApplyAudit?.DirtyStateAfter,
                null);
        }

        /// <summary> Creates a failed input resolution result. </summary>
        public static UnityBuildProfileInputResolutionResult Failure (
            IpcError error,
            IpcUnityBuildProfileInput? unityBuildProfile = null,
            UnityEditorObservation? lifecycleBefore = null,
            IpcBuildDirtyState? dirtyState = null)
        {
            return new UnityBuildProfileInputResolutionResult(
                false,
                null,
                null,
                unityBuildProfile,
                lifecycleBefore,
                dirtyState,
                error);
        }
    }
}
