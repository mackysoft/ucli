using System;
using System.IO;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityDaemonBootstrapContextTests
    {
        private static readonly Guid SessionGenerationId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly DateTimeOffset SessionIssuedAtUtc =
            new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero);

        [Test]
        [Category("Size.Small")]
        public void FromWire_WithAbsolutePaths_ReturnsGuardedBootstrapContext ()
        {
            var repositoryRoot = AbsolutePath.Parse(Path.Combine(
                Path.GetTempPath(),
                "ucli-daemon-bootstrap-context"));
            var sessionPath = ContainedPath.Create(
                repositoryRoot,
                RootRelativePath.Parse("sessions/session.json")).Target;
            var arguments = CreateArguments(repositoryRoot.Value, sessionPath.Value);

            var context = UnityDaemonBootstrapContext.FromWire(arguments);

            Assert.That(context.RepositoryRoot, Is.EqualTo(repositoryRoot));
            Assert.That(context.SessionPath, Is.EqualTo(sessionPath));
            Assert.That(context.ProjectFingerprint, Is.EqualTo(arguments.ProjectFingerprint));
            Assert.That(context.SessionGenerationId, Is.EqualTo(SessionGenerationId));
            Assert.That(context.SessionIssuedAtUtc, Is.EqualTo(SessionIssuedAtUtc));
            Assert.That(context.EndpointBinding.Endpoint, Is.EqualTo(arguments.Endpoint));
            if (arguments.Endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
            {
                Assert.That(
                    context.EndpointBinding.TryGetUnixDomainSocketPath(out var unixDomainSocketPath),
                    Is.True);
                Assert.That(unixDomainSocketPath.Value, Is.EqualTo(arguments.Endpoint.Address));
            }
        }

        [TestCase("repositoryRoot")]
        [TestCase("sessionPath")]
        [Category("Size.Small")]
        public void FromWire_WithRelativePath_ThrowsPathValidationException (string relativeProperty)
        {
            var absoluteRoot = AbsolutePath.Parse(Path.Combine(
                Path.GetTempPath(),
                "ucli-daemon-bootstrap-context"));
            var arguments = CreateArguments(
                relativeProperty == "repositoryRoot" ? "relative/repository" : absoluteRoot.Value,
                relativeProperty == "sessionPath" ? "relative/session.json" : absoluteRoot.Value);

            var exception = Assert.Throws<PathValidationException>(() =>
                UnityDaemonBootstrapContext.FromWire(arguments));

            Assert.That(
                exception!.Failure.Kind,
                Is.EqualTo(PathValidationFailureKind.ExpectedAbsolutePath));
        }

        private static IpcDaemonBootstrapArguments CreateArguments (
            string repositoryRoot,
            string sessionPath)
        {
            return new IpcDaemonBootstrapArguments(
                RepositoryRoot: repositoryRoot,
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("bootstrap-context"),
                SessionPath: sessionPath,
                SessionGenerationId: SessionGenerationId,
                SessionIssuedAtUtc: SessionIssuedAtUtc,
                Endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-bootstrap-context"));
        }
    }
}
