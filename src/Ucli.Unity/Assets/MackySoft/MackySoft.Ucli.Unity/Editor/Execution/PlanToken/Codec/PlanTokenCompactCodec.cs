using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Encodes and decodes compact plan-token values. </summary>
    internal static class PlanTokenCompactCodec
    {
        public const string TokenType = "ucli-plan-token";

        public const string TokenAlgorithm = "HS256";

        public const string TokenKeyId = "v1";

        public const int TokenVersion = 1;

        internal const int MinimumSigningKeyByteLength = 32;

        // The v1 payload has fixed identifiers, three 64-character digests, two timestamps, and one
        // 22-character nonce. A 1024-character segment bounds decoded JSON to 768 bytes while leaving
        // room for the JSON field names and delimiters.
        internal const int MaximumPayloadSegmentLength = 1024;

        private const int SignatureSegmentLength = 43;

        private const string CanonicalHeaderJson = "{\"alg\":\"" + TokenAlgorithm
            + "\",\"kid\":\"" + TokenKeyId
            + "\",\"typ\":\"" + TokenType + "\"}";

        private static readonly string CanonicalHeaderSegment = Base64UrlCodec.Encode(
            Encoding.UTF8.GetBytes(CanonicalHeaderJson));

        /// <summary> Gets the maximum accepted token length from the fixed header, bounded payload, separators, and HMAC-SHA256 signature. </summary>
        internal static int MaximumTokenLength => CanonicalHeaderSegment.Length
            + 1
            + MaximumPayloadSegmentLength
            + 1
            + SignatureSegmentLength;

        /// <summary> Creates a signed compact token string from payload values. </summary>
        /// <param name="signingKey"> The HMAC signing key. </param>
        /// <param name="payload"> The token payload values. </param>
        /// <returns> A compact token accepted by <see cref="TryDecodeToken" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="signingKey" /> or <paramref name="payload" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when the signing key is shorter than 32 bytes or the payload cannot fit in the accepted compact-token boundary. </exception>
        public static string CreateSignedToken (
            byte[] signingKey,
            PlanTokenPayload payload)
        {
            ValidateSigningKey(signingKey);

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var payloadBytes = CreatePayloadJsonBytes(payload);
            var payloadSegment = Base64UrlCodec.Encode(payloadBytes);
            if (payloadSegment.Length > MaximumPayloadSegmentLength)
            {
                throw new ArgumentException(
                    $"Plan-token payload exceeds the maximum encoded length of {MaximumPayloadSegmentLength} characters.",
                    nameof(payload));
            }

            var signingInput = CanonicalHeaderSegment + "." + payloadSegment;
            var signature = ComputeSignature(signingInput, signingKey);
            var signatureSegment = Base64UrlCodec.Encode(signature);
            return signingInput + "." + signatureSegment;
        }

        /// <summary> Attempts to decode compact token string into structured model. </summary>
        /// <param name="token"> The canonical three-segment token string whose length does not exceed <see cref="MaximumTokenLength" />. </param>
        /// <param name="decodedToken"> The decoded token when parse succeeds. </param>
        /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryDecodeToken (
            string? token,
            [NotNullWhen(true)] out PlanTokenDecodedToken? decodedToken)
        {
            decodedToken = null;
            if (token == null || token.Length == 0 || token.Length > MaximumTokenLength)
            {
                return false;
            }

            if (!TryParseTokenParts(token, out var payloadSegment, out var signatureSegment))
            {
                return false;
            }

            if (!Base64UrlCodec.TryDecode(payloadSegment, out var payloadBytes)
                || !Base64UrlCodec.TryDecode(signatureSegment, out var signatureBytes)
                || signatureBytes.Length != PlanTokenDecodedToken.SignatureByteLength)
            {
                return false;
            }

            if (!TryReadPayload(payloadBytes, out var payload))
            {
                return false;
            }

            decodedToken = PlanTokenDecodedToken.Create(
                payloadSegment,
                signatureBytes,
                payload);
            return true;
        }

        /// <summary> Verifies compact-token signature against one signing key. </summary>
        /// <param name="decodedToken"> The decoded token model. </param>
        /// <param name="signingKey"> The signing key bytes. </param>
        /// <returns> <see langword="true" /> when signature matches; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="decodedToken" /> or <paramref name="signingKey" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="signingKey" /> is shorter than 32 bytes. </exception>
        public static bool VerifySignature (
            PlanTokenDecodedToken decodedToken,
            byte[] signingKey)
        {
            if (decodedToken == null)
            {
                throw new ArgumentNullException(nameof(decodedToken));
            }

            ValidateSigningKey(signingKey);

            var signingInput = CanonicalHeaderSegment + "." + decodedToken.PayloadSegment;
            var expectedSignature = ComputeSignature(signingInput, signingKey);
            return CryptographicOperations.FixedTimeEquals(expectedSignature, decodedToken.SignatureBytes);
        }

        /// <summary> Creates compact-token payload JSON bytes. </summary>
        /// <param name="payload"> The payload values. </param>
        /// <returns> The payload JSON bytes. </returns>
        private static byte[] CreatePayloadJsonBytes (PlanTokenPayload payload)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("v", TokenVersion);
                writer.WriteString("kid", TokenKeyId);
                writer.WriteString("projectFingerprint", payload.ProjectFingerprint.ToString());
                writer.WriteString("requestDigest", payload.RequestDigest.ToString());
                writer.WriteString("compiledExecutionDigest", payload.CompiledExecutionDigest.ToString());
                writer.WriteString("stateFingerprint", payload.StateFingerprint.ToString());
                writer.WriteString("issuedAtUtc", payload.IssuedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("expiresAtUtc", payload.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("nonce", payload.Nonce.ToString());
                writer.WriteEndObject();
                writer.Flush();
            }

            return stream.ToArray();
        }

        /// <summary> Computes HMAC signature bytes for one compact-token signing input. </summary>
        /// <param name="signingInput"> The compact signing input text. </param>
        /// <param name="signingKey"> The signing key bytes. </param>
        /// <returns> The HMAC-SHA256 signature bytes. </returns>
        private static byte[] ComputeSignature (
            string signingInput,
            byte[] signingKey)
        {
            var signingInputBytes = Encoding.UTF8.GetBytes(signingInput);
            using var hmac = new HMACSHA256(signingKey);
            return hmac.ComputeHash(signingInputBytes);
        }

        private static void ValidateSigningKey (byte[] signingKey)
        {
            if (signingKey == null)
            {
                throw new ArgumentNullException(nameof(signingKey));
            }

            if (signingKey.Length < MinimumSigningKeyByteLength)
            {
                throw new ArgumentException(
                    $"Plan-token signing key must contain at least {MinimumSigningKeyByteLength} bytes.",
                    nameof(signingKey));
            }
        }

        /// <summary> Parses compact-token segment strings. </summary>
        /// <param name="token"> The compact token string. </param>
        /// <param name="payload"> The payload segment. </param>
        /// <param name="signature"> The signature segment. </param>
        /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryParseTokenParts (
            string token,
            out string payload,
            out string signature)
        {
            payload = string.Empty;
            signature = string.Empty;

            var firstSeparatorIndex = token.IndexOf('.');
            if (firstSeparatorIndex != CanonicalHeaderSegment.Length
                || !token.StartsWith(CanonicalHeaderSegment, StringComparison.Ordinal))
            {
                return false;
            }

            var secondSeparatorIndex = token.IndexOf('.', firstSeparatorIndex + 1);
            if (secondSeparatorIndex < 0 || token.IndexOf('.', secondSeparatorIndex + 1) >= 0)
            {
                return false;
            }

            var payloadLength = secondSeparatorIndex - firstSeparatorIndex - 1;
            var signatureLength = token.Length - secondSeparatorIndex - 1;
            if (payloadLength <= 0
                || payloadLength > MaximumPayloadSegmentLength
                || signatureLength != SignatureSegmentLength)
            {
                return false;
            }

            payload = token.Substring(firstSeparatorIndex + 1, payloadLength);
            signature = token.Substring(secondSeparatorIndex + 1, signatureLength);
            return true;
        }

        /// <summary> Attempts to read token payload from JSON bytes. </summary>
        /// <param name="payloadBytes"> The payload JSON bytes. </param>
        /// <param name="payload"> The parsed payload model. </param>
        /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryReadPayload (
            ReadOnlyMemory<byte> payloadBytes,
            [NotNullWhen(true)] out PlanTokenPayload? payload)
        {
            payload = null;
            try
            {
                using var document = JsonDocument.Parse(payloadBytes);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!root.TryGetProperty("v", out var versionElement)
                    || !versionElement.TryGetInt32(out var version))
                {
                    return false;
                }

                var kid = PlanTokenJsonUtilities.TryReadString(root, "kid");
                var projectFingerprint = PlanTokenJsonUtilities.TryReadString(root, "projectFingerprint");
                var requestDigest = PlanTokenJsonUtilities.TryReadString(root, "requestDigest");
                var compiledExecutionDigest = PlanTokenJsonUtilities.TryReadString(root, "compiledExecutionDigest");
                var stateFingerprint = PlanTokenJsonUtilities.TryReadString(root, "stateFingerprint");
                var issuedAt = PlanTokenJsonUtilities.TryReadString(root, "issuedAtUtc");
                var expiresAt = PlanTokenJsonUtilities.TryReadString(root, "expiresAtUtc");
                var nonce = PlanTokenJsonUtilities.TryReadString(root, "nonce");

                if (version != TokenVersion
                    || !string.Equals(kid, TokenKeyId, StringComparison.Ordinal)
                    || !ProjectFingerprint.TryParse(projectFingerprint, out var parsedProjectFingerprint)
                    || !Sha256Digest.TryParse(requestDigest, out var parsedRequestDigest)
                    || !Sha256Digest.TryParse(compiledExecutionDigest, out var parsedCompiledExecutionDigest)
                    || !Sha256Digest.TryParse(stateFingerprint, out var parsedStateFingerprint)
                    || !TryReadCanonicalUtcTimestamp(issuedAt, out var issuedAtUtc)
                    || !TryReadCanonicalUtcTimestamp(expiresAt, out var expiresAtUtc)
                    || !PlanTokenNonce.TryParse(nonce, out var parsedNonce))
                {
                    return false;
                }

                payload = new PlanTokenPayload(
                    projectFingerprint: parsedProjectFingerprint,
                    requestDigest: parsedRequestDigest,
                    compiledExecutionDigest: parsedCompiledExecutionDigest,
                    stateFingerprint: parsedStateFingerprint,
                    issuedAtUtc: issuedAtUtc,
                    expiresAtUtc: expiresAtUtc,
                    nonce: parsedNonce);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadCanonicalUtcTimestamp (
            string? value,
            out DateTimeOffset timestamp)
        {
            timestamp = default;
            return DateTimeOffset.TryParseExact(
                    value,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out timestamp)
                && timestamp != default
                && timestamp.Offset == TimeSpan.Zero
                && string.Equals(
                    value,
                    timestamp.ToString("O", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
        }
    }
}
