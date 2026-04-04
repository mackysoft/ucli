using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        /// <summary> Creates a signed compact token string from payload values. </summary>
        /// <param name="signingKey"> The HMAC signing key. </param>
        /// <param name="payload"> The token payload values. </param>
        /// <returns> The compact token string. </returns>
        public static string CreateSignedToken (
            byte[] signingKey,
            PlanTokenPayload payload)
        {
            var headerBytes = CreateHeaderJsonBytes();
            var payloadBytes = CreatePayloadJsonBytes(payload);
            var headerSegment = Base64UrlCodec.Encode(headerBytes);
            var payloadSegment = Base64UrlCodec.Encode(payloadBytes);
            var signingInput = headerSegment + "." + payloadSegment;
            var signature = ComputeSignature(signingInput, signingKey);
            var signatureSegment = Base64UrlCodec.Encode(signature);
            return signingInput + "." + signatureSegment;
        }

        /// <summary> Attempts to decode compact token string into structured model. </summary>
        /// <param name="token"> The compact token string. </param>
        /// <param name="decodedToken"> The decoded token when parse succeeds. </param>
        /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryDecodeToken (
            string token,
            out PlanTokenDecodedToken decodedToken)
        {
            decodedToken = default;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (!TryParseTokenParts(token, out var headerSegment, out var payloadSegment, out var signatureSegment))
            {
                return false;
            }

            if (!Base64UrlCodec.TryDecode(headerSegment, out var headerBytes)
                || !Base64UrlCodec.TryDecode(payloadSegment, out var payloadBytes)
                || !Base64UrlCodec.TryDecode(signatureSegment, out var signatureBytes))
            {
                return false;
            }

            if (!TryReadHeader(headerBytes, out var header))
            {
                return false;
            }

            if (!TryReadPayload(payloadBytes, out var payload))
            {
                return false;
            }

            decodedToken = new PlanTokenDecodedToken(
                HeaderSegment: headerSegment,
                PayloadSegment: payloadSegment,
                SignatureBytes: signatureBytes,
                Header: header,
                Payload: payload);
            return true;
        }

        /// <summary> Determines whether token header and payload use supported values. </summary>
        /// <param name="decodedToken"> The decoded token model. </param>
        /// <returns> <see langword="true" /> when token values are supported; otherwise <see langword="false" />. </returns>
        public static bool IsSupported (PlanTokenDecodedToken decodedToken)
        {
            if (decodedToken == null)
            {
                throw new ArgumentNullException(nameof(decodedToken));
            }

            return string.Equals(decodedToken.Header.Algorithm, TokenAlgorithm, StringComparison.Ordinal)
                && string.Equals(decodedToken.Header.Type, TokenType, StringComparison.Ordinal)
                && string.Equals(decodedToken.Header.KeyId, TokenKeyId, StringComparison.Ordinal)
                && string.Equals(decodedToken.Payload.KeyId, TokenKeyId, StringComparison.Ordinal)
                && decodedToken.Payload.Version == TokenVersion;
        }

        /// <summary> Verifies compact-token signature against one signing key. </summary>
        /// <param name="decodedToken"> The decoded token model. </param>
        /// <param name="signingKey"> The signing key bytes. </param>
        /// <returns> <see langword="true" /> when signature matches; otherwise <see langword="false" />. </returns>
        public static bool VerifySignature (
            PlanTokenDecodedToken decodedToken,
            byte[] signingKey)
        {
            if (decodedToken == null)
            {
                throw new ArgumentNullException(nameof(decodedToken));
            }

            if (signingKey == null)
            {
                throw new ArgumentNullException(nameof(signingKey));
            }

            var expectedSignature = ComputeSignature(decodedToken.SigningInput, signingKey);
            return CryptographicOperations.FixedTimeEquals(expectedSignature, decodedToken.SignatureBytes.Span);
        }

        /// <summary> Creates one random nonce string for token payload uniqueness. </summary>
        /// <returns> The generated nonce string. </returns>
        public static string CreateNonce ()
        {
            var nonceBytes = new byte[16];
            RandomNumberGenerator.Fill(nonceBytes);
            return Base64UrlCodec.Encode(nonceBytes);
        }

        /// <summary> Creates compact-token header JSON bytes. </summary>
        /// <returns> The header JSON bytes. </returns>
        private static byte[] CreateHeaderJsonBytes ()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteString("alg", TokenAlgorithm);
                writer.WriteString("kid", TokenKeyId);
                writer.WriteString("typ", TokenType);
                writer.WriteEndObject();
                writer.Flush();
            }

            return stream.ToArray();
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
                writer.WriteNumber("v", payload.Version);
                writer.WriteString("kid", payload.KeyId);
                writer.WriteString("projectFingerprint", payload.ProjectFingerprint);
                writer.WriteString("requestDigest", payload.RequestDigest);
                writer.WriteString("compiledExecutionDigest", payload.CompiledExecutionDigest);
                writer.WriteString("stateFingerprint", payload.StateFingerprint);
                writer.WriteString("issuedAtUtc", payload.IssuedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("expiresAtUtc", payload.ExpiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("nonce", payload.Nonce);
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

        /// <summary> Parses compact-token segment strings. </summary>
        /// <param name="token"> The compact token string. </param>
        /// <param name="header"> The header segment. </param>
        /// <param name="payload"> The payload segment. </param>
        /// <param name="signature"> The signature segment. </param>
        /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryParseTokenParts (
            string token,
            out string header,
            out string payload,
            out string signature)
        {
            header = string.Empty;
            payload = string.Empty;
            signature = string.Empty;

            var segments = token.Split('.');
            if (segments.Length != 3)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(segments[0])
                || string.IsNullOrWhiteSpace(segments[1])
                || string.IsNullOrWhiteSpace(segments[2]))
            {
                return false;
            }

            header = segments[0];
            payload = segments[1];
            signature = segments[2];
            return true;
        }

        /// <summary> Attempts to read token header from JSON bytes. </summary>
        /// <param name="headerBytes"> The header JSON bytes. </param>
        /// <param name="header"> The parsed header model. </param>
        /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryReadHeader (
            ReadOnlyMemory<byte> headerBytes,
            out PlanTokenHeader header)
        {
            header = default;
            try
            {
                using var document = JsonDocument.Parse(headerBytes);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                var alg = PlanTokenJsonUtilities.TryReadString(root, "alg");
                var kid = PlanTokenJsonUtilities.TryReadString(root, "kid");
                var typ = PlanTokenJsonUtilities.TryReadString(root, "typ");
                if (string.IsNullOrWhiteSpace(alg)
                    || string.IsNullOrWhiteSpace(kid)
                    || string.IsNullOrWhiteSpace(typ))
                {
                    return false;
                }

                header = new PlanTokenHeader(alg, kid, typ);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Attempts to read token payload from JSON bytes. </summary>
        /// <param name="payloadBytes"> The payload JSON bytes. </param>
        /// <param name="payload"> The parsed payload model. </param>
        /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryReadPayload (
            ReadOnlyMemory<byte> payloadBytes,
            out PlanTokenPayload payload)
        {
            payload = default;
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

                if (string.IsNullOrWhiteSpace(kid)
                    || string.IsNullOrWhiteSpace(projectFingerprint)
                    || string.IsNullOrWhiteSpace(requestDigest)
                    || string.IsNullOrWhiteSpace(compiledExecutionDigest)
                    || string.IsNullOrWhiteSpace(stateFingerprint)
                    || string.IsNullOrWhiteSpace(issuedAt)
                    || string.IsNullOrWhiteSpace(expiresAt)
                    || string.IsNullOrWhiteSpace(nonce))
                {
                    return false;
                }

                if (!DateTimeOffset.TryParse(
                    issuedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var issuedAtUtc))
                {
                    return false;
                }

                if (!DateTimeOffset.TryParse(
                    expiresAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var expiresAtUtc))
                {
                    return false;
                }

                payload = new PlanTokenPayload(
                    Version: version,
                    KeyId: kid,
                    ProjectFingerprint: projectFingerprint,
                    RequestDigest: requestDigest,
                    CompiledExecutionDigest: compiledExecutionDigest,
                    StateFingerprint: stateFingerprint,
                    IssuedAtUtc: issuedAtUtc,
                    ExpiresAtUtc: expiresAtUtc,
                    Nonce: nonce);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
