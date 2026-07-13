using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one decoded compact token. </summary>
    /// <param name="PayloadSegment"> The base64url-encoded payload segment. </param>
    /// <param name="SignatureBytes"> The decoded signature bytes. </param>
    /// <param name="Payload"> The parsed payload model. </param>
    internal sealed record PlanTokenDecodedToken (
        string PayloadSegment,
        ReadOnlyMemory<byte> SignatureBytes,
        PlanTokenPayload Payload);
}
