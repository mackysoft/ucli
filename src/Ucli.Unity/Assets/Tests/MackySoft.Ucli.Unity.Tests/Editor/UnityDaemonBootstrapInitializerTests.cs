using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityDaemonBootstrapInitializerTests
    {
        [Test]
        [Category("Size.Small")]
        public void ShouldScheduleBootstrap_WhenNotBatchMode_ReturnsFalse ()
        {
            var args = CreateBootstrapArgs();

            var result = Ipc.UnityDaemonBootstrapInitializer.ShouldScheduleBootstrap(
                args,
                isBatchMode: false);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void ShouldScheduleBootstrap_WhenBootstrapArgumentsExistInBatchMode_ReturnsTrue ()
        {
            var args = CreateBootstrapArgs();

            var result = Ipc.UnityDaemonBootstrapInitializer.ShouldScheduleBootstrap(
                args,
                isBatchMode: true);

            Assert.That(result, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void ShouldScheduleBootstrap_WhenBootstrapArgumentsAreMissing_ReturnsFalse ()
        {
            var result = Ipc.UnityDaemonBootstrapInitializer.ShouldScheduleBootstrap(
                new[]
                {
                    "Unity",
                    "-batchmode",
                },
                isBatchMode: true);

            Assert.That(result, Is.False);
        }

        private static IReadOnlyList<string> CreateBootstrapArgs ()
        {
            var args = new List<string>
            {
                "Unity",
                "-batchmode",
            };
            IpcDaemonBootstrapArgumentsCodec.AppendTokens(
                args,
                new IpcDaemonBootstrapArguments(
                    RepositoryRoot: "/repo",
                    ProjectFingerprint: "fingerprint",
                    SessionPath: "/repo/.ucli/session.json",
                    EndpointTransportKind: "unixDomainSocket",
                    EndpointAddress: "/tmp/ucli.sock"));
            return args;
        }
    }
}
