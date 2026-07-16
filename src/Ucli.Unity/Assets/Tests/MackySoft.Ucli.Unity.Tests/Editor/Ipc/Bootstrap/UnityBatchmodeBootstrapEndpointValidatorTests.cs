using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Project;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityBatchmodeBootstrapEndpointValidatorTests
    {
        [TestCase(BatchmodeBootstrapKind.Daemon)]
        [TestCase(BatchmodeBootstrapKind.Oneshot)]
        [Category("Size.Small")]
        public void ResolveValidatedEndpoint_WhenBootstrapIdentityAndEndpointMatch_ReturnsExpectedEndpoint (
            BatchmodeBootstrapKind bootstrapKind)
        {
            var expected = ResolveExpectedContext();
            var arguments = CreateArguments(
                bootstrapKind,
                expected.StorageRoot,
                expected.ProjectFingerprint,
                expected.Endpoint);

            var endpoint = ResolveValidatedEndpoint(bootstrapKind, arguments);

            Assert.That(endpoint, Is.EqualTo(expected.Endpoint));
        }

        [TestCase(BatchmodeBootstrapKind.Daemon, EndpointMismatchKind.Transport)]
        [TestCase(BatchmodeBootstrapKind.Daemon, EndpointMismatchKind.Address)]
        [TestCase(BatchmodeBootstrapKind.Oneshot, EndpointMismatchKind.Transport)]
        [TestCase(BatchmodeBootstrapKind.Oneshot, EndpointMismatchKind.Address)]
        [Category("Size.Small")]
        public void ResolveValidatedEndpoint_WhenDeclaredEndpointDiffersFromExpected_ThrowsInvalidOperationException (
            BatchmodeBootstrapKind bootstrapKind,
            EndpointMismatchKind mismatchKind)
        {
            var expected = ResolveExpectedContext();
            var otherTransportKind = expected.Endpoint.TransportKind == IpcTransportKind.NamedPipe
                ? IpcTransportKind.UnixDomainSocket
                : IpcTransportKind.NamedPipe;
            var endpoint = mismatchKind == EndpointMismatchKind.Transport
                ? otherTransportKind == IpcTransportKind.NamedPipe
                    ? new IpcEndpoint(otherTransportKind, "ucli-foreign-endpoint")
                    : new IpcEndpoint(otherTransportKind, "/tmp/ucli-foreign-endpoint.sock")
                : CreateForeignEndpoint(expected.Endpoint.TransportKind);
            var arguments = CreateArguments(
                bootstrapKind,
                expected.StorageRoot,
                expected.ProjectFingerprint,
                endpoint);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ResolveValidatedEndpoint(bootstrapKind, arguments));

            Assert.That(exception!.Message, Does.Contain("endpoint"));
        }

        [TestCase(BatchmodeBootstrapKind.Daemon)]
        [TestCase(BatchmodeBootstrapKind.Oneshot)]
        [Category("Size.Small")]
        public void ResolveValidatedEndpoint_WhenProjectFingerprintDiffersFromCurrentProject_ThrowsInvalidOperationException (
            BatchmodeBootstrapKind bootstrapKind)
        {
            var expected = ResolveExpectedContext();
            var foreignFingerprint = new ProjectFingerprint(
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
            var arguments = CreateArguments(
                bootstrapKind,
                expected.StorageRoot,
                foreignFingerprint,
                expected.Endpoint);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ResolveValidatedEndpoint(bootstrapKind, arguments));

            Assert.That(exception!.Message, Does.Contain("fingerprint"));
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveValidatedEndpoint_WhenDaemonRepositoryRootDiffersFromCurrentProject_ThrowsInvalidOperationException ()
        {
            var expected = ResolveExpectedContext();
            var arguments = CreateArguments(
                BatchmodeBootstrapKind.Daemon,
                expected.StorageRoot + "-foreign",
                expected.ProjectFingerprint,
                expected.Endpoint);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedDaemonEndpoint(
                    (IpcDaemonBootstrapArguments)arguments));

            Assert.That(exception!.Message, Does.Contain("storage root"));
        }

        private static ExpectedEndpointContext ResolveExpectedContext ()
        {
            var projectRoot = UnityProjectPathResolver.ResolveProjectRootPath();
            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectRoot);
            var projectFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);
            var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
            return new ExpectedEndpointContext(storageRoot, projectFingerprint, endpoint);
        }

        private static IpcEndpoint CreateForeignEndpoint (IpcTransportKind transportKind)
        {
            return transportKind == IpcTransportKind.NamedPipe
                ? new IpcEndpoint(transportKind, "ucli-foreign-endpoint")
                : new IpcEndpoint(transportKind, "/tmp/ucli-foreign-endpoint.sock");
        }

        private static object CreateArguments (
            BatchmodeBootstrapKind bootstrapKind,
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            IpcEndpoint endpoint)
        {
            switch (bootstrapKind)
            {
                case BatchmodeBootstrapKind.Daemon:
                    return new IpcDaemonBootstrapArguments(
                        RepositoryRoot: storageRoot,
                        ProjectFingerprint: projectFingerprint,
                        SessionPath: "/tmp/ucli-session.json",
                        SessionGenerationId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        SessionIssuedAtUtc: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                        Endpoint: endpoint);

                case BatchmodeBootstrapKind.Oneshot:
                    var nowUtc = DateTimeOffset.UtcNow;
                    return new IpcOneshotBootstrapEnvelope(
                        BootstrapId: Guid.NewGuid(),
                        ParentProcess: ProcessLivenessProbe.CaptureCurrentProcess(),
                        ProjectFingerprint: projectFingerprint,
                        SessionToken: IpcSessionToken.CreateRandom(),
                        CreatedAtUtc: nowUtc,
                        ExitDeadlineUtc: nowUtc.AddMinutes(1),
                        Endpoint: endpoint);

                default:
                    throw new ArgumentOutOfRangeException(nameof(bootstrapKind), bootstrapKind, null);
            }
        }

        private static IpcEndpoint ResolveValidatedEndpoint (
            BatchmodeBootstrapKind bootstrapKind,
            object arguments)
        {
            switch (bootstrapKind)
            {
                case BatchmodeBootstrapKind.Daemon:
                    return UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedDaemonEndpoint(
                        (IpcDaemonBootstrapArguments)arguments);
                case BatchmodeBootstrapKind.Oneshot:
                    return UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedOneshotEndpoint(
                        (IpcOneshotBootstrapEnvelope)arguments);
                default:
                    throw new ArgumentOutOfRangeException(nameof(bootstrapKind), bootstrapKind, null);
            }
        }

        public enum BatchmodeBootstrapKind
        {
            Daemon = 0,
            Oneshot = 1,
        }

        public enum EndpointMismatchKind
        {
            Transport = 0,
            Address = 1,
        }

        private sealed class ExpectedEndpointContext
        {
            public ExpectedEndpointContext (
                string storageRoot,
                ProjectFingerprint projectFingerprint,
                IpcEndpoint endpoint)
            {
                StorageRoot = storageRoot;
                ProjectFingerprint = projectFingerprint;
                Endpoint = endpoint;
            }

            public string StorageRoot { get; }

            public ProjectFingerprint ProjectFingerprint { get; }

            public IpcEndpoint Endpoint { get; }
        }
    }
}
