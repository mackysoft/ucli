using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one signed compact-token payload. </summary>
    internal sealed record PlanTokenPayload
    {
        /// <summary> Initializes a payload whose required identifiers, digests, and nonce are present. </summary>
        /// <param name="version"> The positive compact-token format version. </param>
        /// <param name="keyId"> The non-empty signing-key identifier embedded into the token header and payload. </param>
        /// <param name="projectFingerprint"> The project fingerprint captured when the token was issued. </param>
        /// <param name="requestDigest"> The digest of the canonical public request payload. </param>
        /// <param name="compiledExecutionDigest"> The digest of the canonical compiled primitive payload. </param>
        /// <param name="stateFingerprint"> The fingerprint of project state touched by the planned primitives. </param>
        /// <param name="issuedAtUtc"> The timestamp recorded when the token was issued. </param>
        /// <param name="expiresAtUtc"> The timestamp after which the token is no longer accepted. Must be later than <paramref name="issuedAtUtc" />. </param>
        /// <param name="nonce"> The token-unique 16-byte nonce. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="version" /> is not positive. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when a required object value is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="keyId" /> is empty or <paramref name="expiresAtUtc" /> is not later than <paramref name="issuedAtUtc" />. </exception>
        public PlanTokenPayload (
            int version,
            string keyId,
            ProjectFingerprint projectFingerprint,
            Sha256Digest requestDigest,
            Sha256Digest compiledExecutionDigest,
            Sha256Digest stateFingerprint,
            DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc,
            PlanTokenNonce nonce)
        {
            if (version <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(version), version, "Token version must be positive.");
            }

            if (expiresAtUtc <= issuedAtUtc)
            {
                throw new ArgumentException("Token expiration must be later than its issue time.", nameof(expiresAtUtc));
            }

            Version = version;
            KeyId = RequireValue(keyId, nameof(keyId));
            ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
            RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
            CompiledExecutionDigest = compiledExecutionDigest ?? throw new ArgumentNullException(nameof(compiledExecutionDigest));
            StateFingerprint = stateFingerprint ?? throw new ArgumentNullException(nameof(stateFingerprint));
            IssuedAtUtc = issuedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            Nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));
        }

        public int Version { get; }

        public string KeyId { get; }

        public ProjectFingerprint ProjectFingerprint { get; }

        public Sha256Digest RequestDigest { get; }

        public Sha256Digest CompiledExecutionDigest { get; }

        public Sha256Digest StateFingerprint { get; }

        public DateTimeOffset IssuedAtUtc { get; }

        public DateTimeOffset ExpiresAtUtc { get; }

        public PlanTokenNonce Nonce { get; }

        private static string RequireValue (
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"{parameterName} must not be empty.", parameterName);
            }

            return value;
        }
    }
}
