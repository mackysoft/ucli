using System;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents one decoded compact token with an owned HMAC-SHA256 signature snapshot. </summary>
    internal sealed class PlanTokenDecodedToken
    {
        internal const int SignatureByteLength = 32;

        private readonly byte[] signatureBytes;

        private PlanTokenDecodedToken (
            string payloadSegment,
            byte[] signatureBytes,
            PlanTokenPayload payload)
        {
            PayloadSegment = payloadSegment;
            this.signatureBytes = signatureBytes;
            Payload = payload;
        }

        /// <summary> Gets the base64url-encoded payload segment used by signature verification. </summary>
        public string PayloadSegment { get; }

        /// <summary> Gets a read-only view of the owned decoded HMAC-SHA256 signature bytes. </summary>
        public ReadOnlySpan<byte> SignatureBytes => signatureBytes;

        /// <summary> Gets the validated payload for the supported compact-token format. </summary>
        public PlanTokenPayload Payload { get; }

        /// <summary> Creates a decoded token while snapshotting the caller-owned signature bytes. </summary>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="payloadSegment" /> or <paramref name="payload" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when the payload segment is not canonical unpadded base64url or the signature length is not 32 bytes. </exception>
        public static PlanTokenDecodedToken Create (
            string payloadSegment,
            ReadOnlySpan<byte> signatureBytes,
            PlanTokenPayload payload)
        {
            if (payloadSegment == null)
            {
                throw new ArgumentNullException(nameof(payloadSegment));
            }

            if (!Base64UrlCodec.IsCanonical(payloadSegment))
            {
                throw new ArgumentException(
                    "Plan-token payload segment must be canonical unpadded base64url text.",
                    nameof(payloadSegment));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (signatureBytes.Length != SignatureByteLength)
            {
                throw new ArgumentException(
                    $"Plan-token signature must contain exactly {SignatureByteLength} bytes.",
                    nameof(signatureBytes));
            }

            return new PlanTokenDecodedToken(
                payloadSegment,
                signatureBytes.ToArray(),
                payload);
        }
    }
}
