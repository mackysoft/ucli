using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one compact-token payload model. </summary>
    /// <param name="Version"> The token format version. </param>
    /// <param name="KeyId"> The key identifier. </param>
    /// <param name="ProjectFingerprint"> The project fingerprint marker. </param>
    /// <param name="RequestDigest"> The request digest marker. </param>
    /// <param name="StateFingerprint"> The state fingerprint marker. </param>
    /// <param name="IssuedAtUtc"> The token issue time. </param>
    /// <param name="ExpiresAtUtc"> The token expiration time. </param>
    /// <param name="Nonce"> The nonce value. </param>
    internal sealed record PlanTokenPayload (
        int Version,
        string KeyId,
        string ProjectFingerprint,
        string RequestDigest,
        string StateFingerprint,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        string Nonce);
}