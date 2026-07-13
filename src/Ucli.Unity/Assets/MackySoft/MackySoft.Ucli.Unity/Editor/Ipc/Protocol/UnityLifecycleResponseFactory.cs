using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Creates Unity Editor observations for IPC protocol responses. </summary>
    internal static class UnityLifecycleResponseFactory
    {
        /// <summary> Creates one protocol observation from Unity Editor environment values. </summary>
        /// <param name="projectIdentity"> The project identity served by the Unity IPC host. </param>
        /// <param name="serverVersion"> The daemon server version string. </param>
        /// <param name="observation"> The normalized Unity Editor observation. </param>
        /// <returns> The protocol observation. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectIdentity" /> or <paramref name="observation" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when a required project identity value or <paramref name="serverVersion" /> is empty or whitespace. </exception>
        public static IpcUnityEditorObservation Create (
            IpcProjectIdentity projectIdentity,
            string serverVersion,
            UnityEditorObservation observation)
        {
            if (projectIdentity == null)
            {
                throw new ArgumentNullException(nameof(projectIdentity));
            }

            if (observation == null)
            {
                throw new ArgumentNullException(nameof(observation));
            }

            return new IpcUnityEditorObservation(
                serverVersion: serverVersion,
                unityVersion: projectIdentity.UnityVersion,
                projectFingerprint: projectIdentity.ProjectFingerprint,
                state: observation.State,
                observedAtUtc: observation.ObservedAtUtc,
                actionRequired: observation.ActionRequired,
                primaryDiagnostic: observation.PrimaryDiagnostic);
        }
    }
}
