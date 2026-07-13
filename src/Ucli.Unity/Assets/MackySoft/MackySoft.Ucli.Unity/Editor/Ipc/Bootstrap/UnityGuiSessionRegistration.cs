using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents a validated persisted GUI daemon session registration. </summary>
    internal sealed class UnityGuiSessionRegistration
    {
        public UnityGuiSessionRegistration (
            string sessionPath,
            string sessionLockPath,
            IpcSessionToken sessionToken,
            string projectFingerprint,
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

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
            }

            SessionPath = sessionPath;
            SessionLockPath = sessionLockPath;
            SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            ProjectFingerprint = projectFingerprint;
            IssuedAtUtc = issuedAtUtc;
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            CanShutdownProcess = canShutdownProcess;
        }

        public string SessionPath { get; }

        public string SessionLockPath { get; }

        public IpcSessionToken SessionToken { get; }

        public string ProjectFingerprint { get; }

        public DateTimeOffset IssuedAtUtc { get; }

        public IpcEndpoint Endpoint { get; }

        public bool CanShutdownProcess { get; }
    }
}
