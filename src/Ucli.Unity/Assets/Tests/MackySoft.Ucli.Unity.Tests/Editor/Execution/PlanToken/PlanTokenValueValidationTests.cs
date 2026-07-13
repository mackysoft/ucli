using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PlanTokenValueValidationTests
    {
        private static readonly Sha256Digest RequestDigest = Sha256Digest.Parse(new string('a', 64));

        private static readonly Sha256Digest CompiledExecutionDigest = Sha256Digest.Parse(new string('b', 64));

        private static readonly Sha256Digest StateFingerprint = Sha256Digest.Parse(new string('c', 64));

        private const string ValidNonce = "AAAAAAAAAAAAAAAAAAAAAA";

        [Test]
        [Category("Size.Small")]
        public void EnvironmentSnapshot_WithNullProjectFingerprint_ThrowsArgumentNullException ()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new PlanTokenEnvironmentSnapshot(
                projectRoot: "/repo/UnityProject",
                repositoryRoot: "/repo",
                projectFingerprint: null!,
                unityVersion: "6000.0.0f1",
                compileState: "ready",
                domainReloadGeneration: "generation-1"));

            Assert.That(exception!.ParamName, Is.EqualTo("projectFingerprint"));
        }

        [Test]
        [Category("Size.Small")]
        public void Payload_WithNullProjectFingerprint_ThrowsArgumentNullException ()
        {
            var issuedAtUtc = new DateTimeOffset(2026, 7, 13, 1, 2, 3, TimeSpan.Zero);

            var exception = Assert.Throws<ArgumentNullException>(() => new PlanTokenPayload(
                version: 1,
                keyId: "v1",
                projectFingerprint: null!,
                requestDigest: RequestDigest,
                compiledExecutionDigest: CompiledExecutionDigest,
                stateFingerprint: StateFingerprint,
                issuedAtUtc: issuedAtUtc,
                expiresAtUtc: issuedAtUtc.AddMinutes(15),
                nonce: new PlanTokenNonce(ValidNonce)));

            Assert.That(exception!.ParamName, Is.EqualTo("projectFingerprint"));
        }

        [TestCase("requestDigest")]
        [TestCase("compiledExecutionDigest")]
        [TestCase("stateFingerprint")]
        [Category("Size.Small")]
        public void CompactCodec_WhenDigestIsNotCanonical_RejectsPayload (string digestPropertyName)
        {
            var properties = CreateValidPayloadProperties();
            properties[digestPropertyName] = new string('A', 64);

            var token = CreateUnsignedToken(properties);

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void CompactCodec_WhenCompiledExecutionDigestIsMissing_RejectsPayload ()
        {
            var properties = CreateValidPayloadProperties();
            properties.Remove("compiledExecutionDigest");

            var token = CreateUnsignedToken(properties);

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [TestCase("AAAAAAAAAAAAAAAAAAAAA")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAAAA")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAA=")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAA+")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAAB")]
        [Category("Size.Small")]
        public void Nonce_WhenTextIsNotCanonical_ThrowsArgumentException (string nonce)
        {
            var exception = Assert.Throws<ArgumentException>(() => new PlanTokenNonce(nonce));

            Assert.That(exception!.ParamName, Is.EqualTo("value"));
        }

        [Test]
        [Category("Size.Small")]
        public void CompactCodec_WhenNonceIsNotCanonical_RejectsPayload ()
        {
            var properties = CreateValidPayloadProperties();
            properties["nonce"] = "AAAAAAAAAAAAAAAAAAAAAB";

            var token = CreateUnsignedToken(properties);

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void CreateSignedToken_WhenPayloadKeyIdIsUnsupported_ThrowsArgumentException ()
        {
            var payload = CreatePayload(keyId: "v2");

            var exception = Assert.Throws<ArgumentException>(() =>
                PlanTokenCompactCodec.CreateSignedToken(new byte[32], payload));

            Assert.That(exception!.ParamName, Is.EqualTo("payload"));
        }

        [Test]
        [Category("Size.Small")]
        public void CreateSignedToken_WhenPayloadVersionIsUnsupported_ThrowsArgumentException ()
        {
            var payload = CreatePayload(version: PlanTokenCompactCodec.TokenVersion + 1);

            var exception = Assert.Throws<ArgumentException>(() =>
                PlanTokenCompactCodec.CreateSignedToken(new byte[32], payload));

            Assert.That(exception!.ParamName, Is.EqualTo("payload"));
        }

        [Test]
        [Category("Size.Small")]
        public void CreateSignedToken_WhenSigningKeyIsNull_ThrowsArgumentNullException ()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                PlanTokenCompactCodec.CreateSignedToken(null!, CreatePayload()));

            Assert.That(exception!.ParamName, Is.EqualTo("signingKey"));
        }

        [TestCase(0)]
        [TestCase(31)]
        [Category("Size.Small")]
        public void CreateSignedToken_WhenSigningKeyIsTooShort_ThrowsArgumentException (int keyByteLength)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                PlanTokenCompactCodec.CreateSignedToken(new byte[keyByteLength], CreatePayload()));

            Assert.That(exception!.ParamName, Is.EqualTo("signingKey"));
        }

        [Test]
        [Category("Size.Small")]
        public void CreateSignedToken_WhenPayloadIsNull_ThrowsArgumentNullException ()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                PlanTokenCompactCodec.CreateSignedToken(new byte[32], null!));

            Assert.That(exception!.ParamName, Is.EqualTo("payload"));
        }

        [Test]
        [Category("Size.Small")]
        public void CompactCodec_WhenOversizedTokenContainsManySeparators_RejectsWithoutInputSizedAllocation ()
        {
            const int IterationCount = 4;
            var token = new string('.', PlanTokenCompactCodec.MaximumTokenLength + 1);
            _ = PlanTokenCompactCodec.TryDecodeToken(string.Empty, out _);
            _ = GC.GetAllocatedBytesForCurrentThread();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var rejectedCount = 0;

            for (var index = 0; index < IterationCount; index++)
            {
                if (!PlanTokenCompactCodec.TryDecodeToken(token, out _))
                {
                    rejectedCount++;
                }
            }

            var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(rejectedCount, Is.EqualTo(IterationCount));
            Assert.That(allocatedAfter - allocatedBefore, Is.LessThan(token.Length));
        }

        [Test]
        [Category("Size.Small")]
        public void CompactCodec_WhenPayloadSegmentExceedsMaximum_RejectsToken ()
        {
            var segments = CreateValidToken().Split('.');
            var payloadJson = JsonSerializer.Serialize(CreateValidPayloadProperties()) + new string(' ', 512);
            var oversizedPayloadSegment = Base64UrlCodec.Encode(Encoding.UTF8.GetBytes(payloadJson));
            Assert.That(oversizedPayloadSegment.Length, Is.GreaterThan(PlanTokenCompactCodec.MaximumPayloadSegmentLength));
            var token = string.Join(".", segments[0], oversizedPayloadSegment, segments[2]);

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [TestCase(2)]
        [TestCase(4)]
        [Category("Size.Small")]
        public void CompactCodec_WhenSegmentCountIsNotThree_RejectsToken (int segmentCount)
        {
            var segments = CreateValidToken().Split('.');
            var token = segmentCount == 2
                ? string.Join(".", segments[0], segments[1])
                : string.Join(".", segments[0], segments[1], segments[2], "extra");

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void CompactCodec_WhenSignatureUsesBase64Padding_RejectsToken ()
        {
            var segments = CreateValidToken().Split('.');
            var token = string.Join(".", segments[0], segments[1], segments[2] + "=");

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [TestCase('+')]
        [TestCase('/')]
        [Category("Size.Small")]
        public void CompactCodec_WhenSignatureUsesStandardBase64Character_RejectsToken (char character)
        {
            var segments = CreateValidToken().Split('.');
            var signature = character + segments[2].Substring(1);
            var token = string.Join(".", segments[0], segments[1], signature);

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        [Category("Size.Small")]
        public void CompactCodec_WhenTokenHasOuterWhitespace_RejectsToken (bool prefixWhitespace)
        {
            var token = CreateValidToken();
            token = prefixWhitespace ? " " + token : token + " ";

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void CompactCodec_WhenSignatureHasNonCanonicalTrailingBits_RejectsToken ()
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
            var segments = CreateValidToken().Split('.');
            var canonicalLastIndex = alphabet.IndexOf(segments[2][segments[2].Length - 1]);
            Assert.That(canonicalLastIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(canonicalLastIndex % 4, Is.Zero);
            var signature = segments[2].Substring(0, segments[2].Length - 1)
                + alphabet[canonicalLastIndex + 1];
            var token = string.Join(".", segments[0], segments[1], signature);

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Payload_WithNullNonce_ThrowsArgumentNullException ()
        {
            var issuedAtUtc = new DateTimeOffset(2026, 7, 13, 1, 2, 3, TimeSpan.Zero);

            var exception = Assert.Throws<ArgumentNullException>(() => new PlanTokenPayload(
                version: 1,
                keyId: "v1",
                projectFingerprint: ProjectFingerprintTestFactory.Create("plan-token-payload"),
                requestDigest: RequestDigest,
                compiledExecutionDigest: CompiledExecutionDigest,
                stateFingerprint: StateFingerprint,
                issuedAtUtc: issuedAtUtc,
                expiresAtUtc: issuedAtUtc.AddMinutes(15),
                nonce: null!));

            Assert.That(exception!.ParamName, Is.EqualTo("nonce"));
        }

        [Test]
        [Category("Size.Small")]
        public void CompactCodec_WithValidPayload_RoundTripsCanonicalValuesAndVerifiesSignature ()
        {
            var signingKey = new byte[32];
            var token = CreateValidToken(signingKey);

            Assert.That(token.Length, Is.LessThanOrEqualTo(PlanTokenCompactCodec.MaximumTokenLength));
            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out var decodedToken), Is.True);
            Assert.That(decodedToken, Is.Not.Null);
            Assert.That(PlanTokenCompactCodec.VerifySignature(decodedToken!, signingKey), Is.True);
            Assert.That(decodedToken!.Payload.RequestDigest, Is.EqualTo(RequestDigest));
            Assert.That(decodedToken.Payload.CompiledExecutionDigest, Is.EqualTo(CompiledExecutionDigest));
            Assert.That(decodedToken.Payload.StateFingerprint, Is.EqualTo(StateFingerprint));
            Assert.That(decodedToken.Payload.Nonce, Is.EqualTo(new PlanTokenNonce(ValidNonce)));
        }

        [TestCase(0)]
        [TestCase(31)]
        [Category("Size.Small")]
        public void VerifySignature_WhenSigningKeyIsTooShort_ThrowsArgumentException (int keyByteLength)
        {
            var token = CreateValidToken();
            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out var decodedToken), Is.True);

            var exception = Assert.Throws<ArgumentException>(() =>
                PlanTokenCompactCodec.VerifySignature(decodedToken!, new byte[keyByteLength]));

            Assert.That(exception!.ParamName, Is.EqualTo("signingKey"));
        }

        [Test]
        [Category("Size.Small")]
        public void Nonce_Create_ReturnsCanonicalSixteenByteValue ()
        {
            var nonce = PlanTokenNonce.Create();

            Assert.That(nonce.ToString(), Has.Length.EqualTo(22));
            Assert.That(PlanTokenNonce.TryParse(nonce.ToString(), out var parsedNonce), Is.True);
            Assert.That(parsedNonce, Is.EqualTo(nonce));
            Assert.That(Base64UrlCodec.TryDecode(nonce.ToString(), out var bytes), Is.True);
            Assert.That(bytes, Has.Length.EqualTo(16));
        }

        private static Dictionary<string, object> CreateValidPayloadProperties ()
        {
            var issuedAtUtc = new DateTimeOffset(2026, 7, 13, 1, 2, 3, TimeSpan.Zero);
            return new Dictionary<string, object>
            {
                ["v"] = PlanTokenCompactCodec.TokenVersion,
                ["kid"] = PlanTokenCompactCodec.TokenKeyId,
                ["projectFingerprint"] = ProjectFingerprintTestFactory.Create("plan-token-json").ToString(),
                ["requestDigest"] = RequestDigest.ToString(),
                ["compiledExecutionDigest"] = CompiledExecutionDigest.ToString(),
                ["stateFingerprint"] = StateFingerprint.ToString(),
                ["issuedAtUtc"] = issuedAtUtc.ToString("O"),
                ["expiresAtUtc"] = issuedAtUtc.AddMinutes(15).ToString("O"),
                ["nonce"] = ValidNonce,
            };
        }

        private static string CreateUnsignedToken (Dictionary<string, object> payloadProperties)
        {
            var headerBytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                alg = PlanTokenCompactCodec.TokenAlgorithm,
                kid = PlanTokenCompactCodec.TokenKeyId,
                typ = PlanTokenCompactCodec.TokenType,
            });
            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payloadProperties);
            return Base64UrlCodec.Encode(headerBytes)
                + "."
                + Base64UrlCodec.Encode(payloadBytes)
                + "."
                + Base64UrlCodec.Encode(new byte[32]);
        }

        private static string CreateValidToken (byte[]? signingKey = null)
        {
            return PlanTokenCompactCodec.CreateSignedToken(signingKey ?? new byte[32], CreatePayload());
        }

        private static PlanTokenPayload CreatePayload (
            string? keyId = null,
            int version = PlanTokenCompactCodec.TokenVersion)
        {
            var issuedAtUtc = new DateTimeOffset(2026, 7, 13, 1, 2, 3, TimeSpan.Zero);
            return new PlanTokenPayload(
                version: version,
                keyId: keyId ?? PlanTokenCompactCodec.TokenKeyId,
                projectFingerprint: ProjectFingerprintTestFactory.Create("plan-token-roundtrip"),
                requestDigest: RequestDigest,
                compiledExecutionDigest: CompiledExecutionDigest,
                stateFingerprint: StateFingerprint,
                issuedAtUtc: issuedAtUtc,
                expiresAtUtc: issuedAtUtc.AddMinutes(15),
                nonce: new PlanTokenNonce(ValidNonce));
        }
    }
}
