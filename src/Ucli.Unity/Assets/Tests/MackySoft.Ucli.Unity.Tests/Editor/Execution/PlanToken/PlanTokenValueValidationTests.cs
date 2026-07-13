using System;
using System.Collections.Generic;
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
            var issuedAtUtc = new DateTimeOffset(2026, 7, 13, 1, 2, 3, TimeSpan.Zero);
            var nonce = new PlanTokenNonce(ValidNonce);
            var payload = new PlanTokenPayload(
                version: PlanTokenCompactCodec.TokenVersion,
                keyId: PlanTokenCompactCodec.TokenKeyId,
                projectFingerprint: ProjectFingerprintTestFactory.Create("plan-token-roundtrip"),
                requestDigest: RequestDigest,
                compiledExecutionDigest: CompiledExecutionDigest,
                stateFingerprint: StateFingerprint,
                issuedAtUtc: issuedAtUtc,
                expiresAtUtc: issuedAtUtc.AddMinutes(15),
                nonce: nonce);
            var signingKey = new byte[32];

            var token = PlanTokenCompactCodec.CreateSignedToken(signingKey, payload);

            Assert.That(PlanTokenCompactCodec.TryDecodeToken(token, out var decodedToken), Is.True);
            Assert.That(decodedToken, Is.Not.Null);
            Assert.That(PlanTokenCompactCodec.VerifySignature(decodedToken!, signingKey), Is.True);
            Assert.That(decodedToken!.Payload.RequestDigest, Is.EqualTo(RequestDigest));
            Assert.That(decodedToken.Payload.CompiledExecutionDigest, Is.EqualTo(CompiledExecutionDigest));
            Assert.That(decodedToken.Payload.StateFingerprint, Is.EqualTo(StateFingerprint));
            Assert.That(decodedToken.Payload.Nonce, Is.EqualTo(nonce));
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
    }
}
