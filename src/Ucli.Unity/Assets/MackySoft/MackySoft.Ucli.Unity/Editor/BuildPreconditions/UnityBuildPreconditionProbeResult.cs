using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents the result of a build precondition probe. </summary>
    /// <param name="IsSuccess"> Whether all preconditions passed. </param>
    /// <param name="Project"> The Unity project identity attached to this probe. </param>
    /// <param name="LifecycleBefore"> The lifecycle snapshot captured before the build attempt. </param>
    /// <param name="DirtyState"> The dirty-state result when checked. </param>
    /// <param name="InputProbe"> The resolved input probe when available. </param>
    /// <param name="ResolvedInput"> The Unity BuildPipeline input when preconditions passed. </param>
    /// <param name="Error"> The precondition error when failed. </param>
    internal sealed record UnityBuildPreconditionProbeResult (
        bool IsSuccess,
        IpcProjectIdentity Project,
        IpcBuildLifecycleSnapshot LifecycleBefore,
        IpcBuildDirtyState? DirtyState,
        IpcBuildInputProbe? InputProbe,
        UnityBuildResolvedInput? ResolvedInput,
        IpcError? Error)
    {
        /// <summary> Creates a successful precondition result. </summary>
        public static UnityBuildPreconditionProbeResult Success (
            IpcProjectIdentity project,
            IpcBuildLifecycleSnapshot lifecycleBefore,
            IpcBuildDirtyState dirtyState,
            IpcBuildInputProbe inputProbe,
            UnityBuildResolvedInput resolvedInput)
        {
            return new UnityBuildPreconditionProbeResult(
                true,
                project,
                lifecycleBefore,
                dirtyState,
                inputProbe,
                resolvedInput,
                null);
        }

        /// <summary> Creates a failed precondition result. </summary>
        public static UnityBuildPreconditionProbeResult Failure (
            IpcProjectIdentity project,
            IpcBuildLifecycleSnapshot lifecycleBefore,
            IpcBuildDirtyState? dirtyState,
            IpcBuildInputProbe? inputProbe,
            IpcError error)
        {
            return new UnityBuildPreconditionProbeResult(
                false,
                project,
                lifecycleBefore,
                dirtyState,
                inputProbe,
                null,
                error);
        }
    }
}
