using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
/// <summary> Represents one signed compact-token payload. </summary>
/// <param name="Version"> The compact-token format version expected by the decoder. </param>
/// <param name="KeyId"> The signing-key identifier embedded into the token header and payload. </param>
/// <param name="ProjectFingerprint"> The stable project fingerprint captured when the token was issued. </param>
/// <param name="RequestDigest"> The digest of the canonical public request payload. </param>
/// <param name="CompiledExecutionDigest"> The digest of the canonical compiled primitive payload. </param>
/// <param name="StateFingerprint"> The fingerprint of project state touched by the planned primitives. </param>
/// <param name="IssuedAtUtc"> The UTC timestamp recorded when the token was issued. </param>
/// <param name="ExpiresAtUtc"> The UTC timestamp after which the token is no longer accepted. </param>
/// <param name="Nonce"> The token-unique nonce used to prevent deterministic replay tokens. </param>
internal sealed record PlanTokenPayload (
        int Version,
        string KeyId,
        string ProjectFingerprint,
        string RequestDigest,
        string CompiledExecutionDigest,
        string StateFingerprint,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        string Nonce);
}
