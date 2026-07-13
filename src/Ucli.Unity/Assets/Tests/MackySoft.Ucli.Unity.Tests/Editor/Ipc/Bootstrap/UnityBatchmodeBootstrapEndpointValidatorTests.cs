using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
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
                ContractLiteralCodec.ToValue(expected.Endpoint.TransportKind),
                expected.Endpoint.Address);

            var endpoint = UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedEndpoint(arguments);

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
            var transportKind = mismatchKind == EndpointMismatchKind.Transport
                ? ContractLiteralCodec.ToValue(otherTransportKind)
                : ContractLiteralCodec.ToValue(expected.Endpoint.TransportKind);
            var address = mismatchKind == EndpointMismatchKind.Address
                ? expected.Endpoint.Address + "-foreign"
                : expected.Endpoint.Address;
            var arguments = CreateArguments(
                bootstrapKind,
                expected.StorageRoot,
                expected.ProjectFingerprint,
                transportKind,
                address);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedEndpoint(arguments));

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
                ContractLiteralCodec.ToValue(expected.Endpoint.TransportKind),
                expected.Endpoint.Address);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedEndpoint(arguments));

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
                ContractLiteralCodec.ToValue(expected.Endpoint.TransportKind),
                expected.Endpoint.Address);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                UnityBatchmodeBootstrapEndpointValidator.ResolveValidatedEndpoint(arguments));

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

        private static IpcBatchmodeBootstrapArguments CreateArguments (
            BatchmodeBootstrapKind bootstrapKind,
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            string endpointTransportKind,
            string endpointAddress)
        {
            switch (bootstrapKind)
            {
                case BatchmodeBootstrapKind.Daemon:
                    return new IpcDaemonBootstrapArguments(
                        RepositoryRoot: storageRoot,
                        ProjectFingerprint: projectFingerprint,
                        SessionPath: "/tmp/ucli-session.json",
                        SessionIssuedAtUtc: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                        EndpointTransportKind: endpointTransportKind,
                        EndpointAddress: endpointAddress);

                case BatchmodeBootstrapKind.Oneshot:
                    return new IpcOneshotBootstrapArguments(
                        ParentProcessId: 1,
                        ProjectFingerprint: projectFingerprint,
                        SessionToken: "session-token",
                        ExitDeadlineUtc: DateTimeOffset.Parse("2026-01-01T00:01:00Z"),
                        EndpointTransportKind: endpointTransportKind,
                        EndpointAddress: endpointAddress);

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
