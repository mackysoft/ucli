using System;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Carries one daemon bootstrap generation after adapting its wire paths to guarded filesystem values.
    /// </summary>
    internal sealed class UnityDaemonBootstrapContext
    {
        /// <summary> Initializes one daemon bootstrap generation from guarded filesystem paths. </summary>
        /// <param name="repositoryRoot"> The guarded absolute repository root. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by the bootstrap generation. </param>
        /// <param name="sessionPath"> The guarded absolute canonical daemon session path. </param>
        /// <param name="sessionGenerationId"> The non-empty session generation identifier. </param>
        /// <param name="sessionIssuedAtUtc"> The non-default UTC session issuance timestamp. </param>
        /// <param name="endpointBinding"> The guarded runtime binding for the IPC endpoint declared by the session generation. </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when a reference-type argument is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sessionGenerationId" /> is empty or
        /// <paramref name="sessionIssuedAtUtc" /> is default or non-UTC.
        /// </exception>
        public UnityDaemonBootstrapContext (
            AbsolutePath repositoryRoot,
            ProjectFingerprint projectFingerprint,
            AbsolutePath sessionPath,
            Guid sessionGenerationId,
            DateTimeOffset sessionIssuedAtUtc,
            UnityIpcEndpointBinding endpointBinding)
        {
            RepositoryRoot = repositoryRoot ?? throw new ArgumentNullException(nameof(repositoryRoot));
            ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            SessionPath = sessionPath ?? throw new ArgumentNullException(nameof(sessionPath));
            if (sessionGenerationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Session generation identifier must not be empty.",
                    nameof(sessionGenerationId));
            }

            if (sessionIssuedAtUtc == default || sessionIssuedAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Session issuance timestamp must be a non-default UTC value.",
                    nameof(sessionIssuedAtUtc));
            }

            SessionGenerationId = sessionGenerationId;
            SessionIssuedAtUtc = sessionIssuedAtUtc;
            EndpointBinding = endpointBinding ?? throw new ArgumentNullException(nameof(endpointBinding));
        }

        /// <summary> Gets the guarded absolute repository root declared by the bootstrap generation. </summary>
        public AbsolutePath RepositoryRoot { get; }

        /// <summary> Gets the project fingerprint served by the bootstrap generation. </summary>
        public ProjectFingerprint ProjectFingerprint { get; }

        /// <summary> Gets the guarded absolute canonical daemon session path. </summary>
        public AbsolutePath SessionPath { get; }

        /// <summary> Gets the identity of the session generation authorized to bootstrap. </summary>
        public Guid SessionGenerationId { get; }

        /// <summary> Gets the UTC session issuance timestamp. </summary>
        public DateTimeOffset SessionIssuedAtUtc { get; }

        /// <summary> Gets the guarded runtime binding for the IPC endpoint declared by the session generation. </summary>
        public UnityIpcEndpointBinding EndpointBinding { get; }

        /// <summary> Adapts one daemon bootstrap wire payload to guarded internal values. </summary>
        /// <param name="arguments"> The validated daemon bootstrap wire payload. </param>
        /// <returns> An internal context whose path properties are guarded absolute values. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="arguments" /> is <see langword="null" />. </exception>
        /// <exception cref="PathValidationException">
        /// Thrown when a declared wire path is not a valid absolute path on the running operating system.
        /// </exception>
        public static UnityDaemonBootstrapContext FromWire (IpcDaemonBootstrapArguments arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return new UnityDaemonBootstrapContext(
                AbsolutePath.Parse(arguments.RepositoryRoot),
                arguments.ProjectFingerprint,
                AbsolutePath.Parse(arguments.SessionPath),
                arguments.SessionGenerationId,
                arguments.SessionIssuedAtUtc,
                UnityIpcEndpointBinding.Create(arguments.Endpoint));
        }
    }
}
