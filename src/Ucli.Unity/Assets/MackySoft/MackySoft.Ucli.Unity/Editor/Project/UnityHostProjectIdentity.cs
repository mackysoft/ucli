using System;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Project
{
    /// <summary>
    /// Represents the current Unity host project as both an internal guarded path
    /// and the corresponding IPC identity.
    /// </summary>
    internal sealed class UnityHostProjectIdentity
    {
        /// <summary> Initializes one host identity from an already validated project root. </summary>
        /// <param name="projectPath"> The guarded absolute Unity project root. </param>
        /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
        /// <param name="unityVersion"> The Unity editor version published through IPC. </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="projectPath" />, <paramref name="projectFingerprint" />, or
        /// <paramref name="unityVersion" /> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="unityVersion" /> violates the IPC identity contract.
        /// </exception>
        public UnityHostProjectIdentity (
            AbsolutePath projectPath,
            ProjectFingerprint projectFingerprint,
            string unityVersion)
        {
            ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            IpcIdentity = new IpcProjectIdentity(
                projectPath.Value,
                projectFingerprint,
                unityVersion);
        }

        /// <summary> Gets the guarded absolute Unity project root. </summary>
        public AbsolutePath ProjectPath { get; }

        /// <summary> Gets the project fingerprint shared by internal and IPC representations. </summary>
        public ProjectFingerprint ProjectFingerprint => IpcIdentity.ProjectFingerprint;

        /// <summary> Gets the wire identity projected into IPC responses and public runner contexts. </summary>
        public IpcProjectIdentity IpcIdentity { get; }
    }
}
