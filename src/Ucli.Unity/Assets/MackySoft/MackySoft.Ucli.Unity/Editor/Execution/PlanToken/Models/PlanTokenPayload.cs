using System;
using MackySoft.Ucli.Contracts;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
/// <summary> Represents one signed compact-token payload. </summary>
/// <param name="Version"> The compact-token format version expected by the decoder. </param>
/// <param name="KeyId"> The signing-key identifier embedded into the token header and payload. </param>
/// <param name="ProjectFingerprint"> The stable project fingerprint captured when the token was issued. </param>
/// <param name="RequestDigest"> The digest of the canonical public request payload. </param>
/// <param name="CompiledExecutionDigest"> The digest of the canonical compiled primitive payload. <see langword="null" /> for legacy <c>v1</c> tokens issued before compiled execution digests were added. </param>
/// <param name="StateFingerprint"> The fingerprint of project state touched by the planned primitives. </param>
/// <param name="IssuedAtUtc"> The UTC timestamp recorded when the token was issued. </param>
/// <param name="ExpiresAtUtc"> The UTC timestamp after which the token is no longer accepted. </param>
/// <param name="Nonce"> The token-unique nonce used to prevent deterministic replay tokens. </param>
internal sealed record PlanTokenPayload
    {
        public PlanTokenPayload (
            int version,
            string keyId,
            ProjectFingerprint projectFingerprint,
            string requestDigest,
            string? compiledExecutionDigest,
            string stateFingerprint,
            DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc,
            string nonce)
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
            RequestDigest = RequireValue(requestDigest, nameof(requestDigest));
            CompiledExecutionDigest = compiledExecutionDigest == null
                ? null
                : RequireValue(compiledExecutionDigest, nameof(compiledExecutionDigest));
            StateFingerprint = RequireValue(stateFingerprint, nameof(stateFingerprint));
            IssuedAtUtc = issuedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            Nonce = RequireValue(nonce, nameof(nonce));
        }

        public int Version { get; }

        public string KeyId { get; }

        public ProjectFingerprint ProjectFingerprint { get; }

        public string RequestDigest { get; }

        public string? CompiledExecutionDigest { get; }

        public string StateFingerprint { get; }

        public DateTimeOffset IssuedAtUtc { get; }

        public DateTimeOffset ExpiresAtUtc { get; }

        public string Nonce { get; }

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
