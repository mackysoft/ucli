using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one decoded compact token. </summary>
    /// <param name="HeaderSegment"> The base64url-encoded header segment. </param>
    /// <param name="PayloadSegment"> The base64url-encoded payload segment. </param>
    /// <param name="SignatureBytes"> The decoded signature bytes. </param>
    /// <param name="Header"> The parsed header model. </param>
    /// <param name="Payload"> The parsed payload model. </param>
    internal sealed record PlanTokenDecodedToken (
        string HeaderSegment,
        string PayloadSegment,
        ReadOnlyMemory<byte> SignatureBytes,
        PlanTokenHeader Header,
        PlanTokenPayload Payload)
    {
        /// <summary> Gets compact-token signing input. </summary>
        public string SigningInput => HeaderSegment + "." + PayloadSegment;
    }
}
