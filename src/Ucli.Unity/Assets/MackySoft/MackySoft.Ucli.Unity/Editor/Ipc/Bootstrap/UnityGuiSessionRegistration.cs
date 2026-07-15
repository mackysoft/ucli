using System;
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
        /// <param name="endpoint"> The IPC endpoint published by this registration. </param>
        /// <param name="canShutdownProcess"> Whether uCLI may shut down the process that owns this registration. </param>
        /// <exception cref="ArgumentNullException"> Thrown when a required reference is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when a path is empty or <paramref name="sessionGenerationId" /> is <see cref="Guid.Empty" />. </exception>
        public UnityGuiSessionRegistration (
            string sessionPath,
            string sessionLockPath,
            Guid sessionGenerationId,
            IpcSessionToken sessionToken,
            ProjectFingerprint projectFingerprint,
            DateTimeOffset issuedAtUtc,
            IpcEndpoint endpoint,
            bool canShutdownProcess)
        {
            if (string.IsNullOrWhiteSpace(sessionPath))
            {
                throw new ArgumentException("Session path must not be empty.", nameof(sessionPath));
            }

            if (string.IsNullOrWhiteSpace(sessionLockPath))
            {
                throw new ArgumentException("Session lock path must not be empty.", nameof(sessionLockPath));
            }

            if (sessionGenerationId == Guid.Empty)
            {
                throw new ArgumentException("Session generation identifier must not be empty.", nameof(sessionGenerationId));
            }

            SessionPath = sessionPath;
            SessionLockPath = sessionLockPath;
            SessionGenerationId = sessionGenerationId;
            SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            IssuedAtUtc = issuedAtUtc;
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            CanShutdownProcess = canShutdownProcess;
        }

        public string SessionPath { get; }

        public string SessionLockPath { get; }

        public Guid SessionGenerationId { get; }

        public IpcSessionToken SessionToken { get; }

        public ProjectFingerprint ProjectFingerprint { get; }

        public DateTimeOffset IssuedAtUtc { get; }

        public IpcEndpoint Endpoint { get; }

        public bool CanShutdownProcess { get; }
    }
}
