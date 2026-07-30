using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcEndpointBindingTests
    {
#if !UNITY_EDITOR_WIN
        [Test]
        [Category("Size.Small")]
        public void Create_WithGuardedUnixDomainSocketEndpoint_RetainsSocketPathWithoutReparsing ()
        {
            var socketPath = AbsolutePath.Parse(
                "/tmp/ucli-endpoint-binding.sock");
            var endpoint = IpcTransportEndpoint.FromUnixSocketPath(socketPath);

            var binding = UnityIpcEndpointBinding.Create(endpoint);

            Assert.That(binding.Endpoint, Is.SameAs(endpoint.Contract));
            Assert.That(
                binding.TryGetUnixDomainSocketPath(out var guardedSocketPath),
                Is.True);
            Assert.That(guardedSocketPath, Is.SameAs(socketPath));
        }
#endif

        [Test]
        [Category("Size.Small")]
        public void Create_WithNamedPipeEndpoint_PreservesOpaqueLogicalName ()
        {
            var endpoint = new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                "ucli-logical-pipe-name");

            var binding = UnityIpcEndpointBinding.Create(endpoint);

            Assert.That(binding.Endpoint, Is.SameAs(endpoint));
            Assert.That(
                binding.TryGetUnixDomainSocketPath(out var guardedSocketPath),
                Is.False);
            Assert.That(guardedSocketPath, Is.Null);
        }

    }
}
