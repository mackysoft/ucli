using System;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents a validated persisted GUI daemon session registration. </summary>
    internal sealed class UnityGuiSessionRegistration
    {
        /// <summary> Initializes one GUI session registration owned by a single non-empty generation identifier. </summary>
        /// <param name="sessionPath"> The non-empty persisted session path. </param>
        /// <param name="sessionLockPath"> The non-empty session publication lock path. </param>
        /// <param name="sessionGenerationId"> The non-empty identity used for exact publication and deletion ownership. </param>
        /// <param name="sessionToken"> The session authorization token. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this registration. </param>
        /// <param name="issuedAtUtc"> The time at which this registration was issued. </param>
        /// <param name="endpointBinding"> The guarded runtime binding for the IPC endpoint published by this registration. </param>
        /// <param name="canShutdownProcess"> Whether uCLI may shut down the process that owns this registration. </param>
        /// <exception cref="ArgumentNullException"> Thrown when a required reference is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when a path is empty or <paramref name="sessionGenerationId" /> is <see cref="Guid.Empty" />. </exception>
        public UnityGuiSessionRegistration (
            AbsolutePath sessionPath,
            AbsolutePath sessionLockPath,
            Guid sessionGenerationId,
            IpcSessionToken sessionToken,
            ProjectFingerprint projectFingerprint,
            DateTimeOffset issuedAtUtc,
            UnityIpcEndpointBinding endpointBinding,
            bool canShutdownProcess)
        {
            if (sessionGenerationId == Guid.Empty)
            {
                throw new ArgumentException("Session generation identifier must not be empty.", nameof(sessionGenerationId));
            }

            SessionPath = sessionPath ?? throw new ArgumentNullException(nameof(sessionPath));
            SessionLockPath = sessionLockPath ?? throw new ArgumentNullException(nameof(sessionLockPath));
            SessionGenerationId = sessionGenerationId;
            SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            IssuedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(issuedAtUtc, nameof(issuedAtUtc));
            EndpointBinding = endpointBinding ?? throw new ArgumentNullException(nameof(endpointBinding));
            CanShutdownProcess = canShutdownProcess;
        }

        public AbsolutePath SessionPath { get; }

        public AbsolutePath SessionLockPath { get; }

        public Guid SessionGenerationId { get; }

        public IpcSessionToken SessionToken { get; }

        public ProjectFingerprint ProjectFingerprint { get; }

        public DateTimeOffset IssuedAtUtc { get; }

        public UnityIpcEndpointBinding EndpointBinding { get; }

        public bool CanShutdownProcess { get; }
    }
}
