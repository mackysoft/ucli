#nullable enable

using System;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one captured runtime snapshot used by plan-token workflows. </summary>
    internal sealed record PlanTokenEnvironmentSnapshot
    {
        /// <summary> Initializes one plan-token environment snapshot. </summary>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="projectRoot" />, <paramref name="repositoryRoot" />, or
        /// <paramref name="projectFingerprint" /> is <see langword="null" />.
        /// </exception>
        public PlanTokenEnvironmentSnapshot (
            AbsolutePath projectRoot,
            AbsolutePath repositoryRoot,
            ProjectFingerprint projectFingerprint,
            string unityVersion,
            IpcCompileState compileState,
            long domainReloadGeneration)
        {
            ProjectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
            RepositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
            ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            UnityVersion = unityVersion;
            CompileState = compileState;
            DomainReloadGeneration = domainReloadGeneration;
        }

        /// <summary> Gets the Unity project root path. </summary>
        public AbsolutePath ProjectRoot { get; init; }

        /// <summary> Gets the repository root path. </summary>
        public AbsolutePath RepositoryRoot { get; init; }

        /// <summary> Gets the deterministic project fingerprint. </summary>
        public ProjectFingerprint ProjectFingerprint { get; init; }

        /// <summary> Gets the current Unity version. </summary>
        public string UnityVersion { get; init; }

        /// <summary> Gets the current compile state. </summary>
        public IpcCompileState CompileState { get; init; }

        /// <summary> Gets the current domain-reload generation marker. </summary>
        public long DomainReloadGeneration { get; init; }
    }
}
