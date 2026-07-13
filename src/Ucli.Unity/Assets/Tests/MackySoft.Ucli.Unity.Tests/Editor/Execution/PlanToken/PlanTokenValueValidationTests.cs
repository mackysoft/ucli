using System;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class PlanTokenValueValidationTests
    {
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
                requestDigest: new string('a', 64),
                compiledExecutionDigest: new string('b', 64),
                stateFingerprint: new string('c', 64),
                issuedAtUtc: issuedAtUtc,
                expiresAtUtc: issuedAtUtc.AddMinutes(15),
                nonce: "nonce"));

            Assert.That(exception!.ParamName, Is.EqualTo("projectFingerprint"));
        }
    }
}
