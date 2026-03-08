using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityBatchmodeBootstrapInitializerTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryParse_WhenDaemonBootstrapArgumentsExist_ReturnsDaemonPayload ()
        {
            var args = CreateDaemonBootstrapArgs();

            var result = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out _);

            Assert.That(result, Is.True);
            Assert.That(bootstrapArguments, Is.TypeOf<IpcDaemonBootstrapArguments>());
        }

        [Test]
        [Category("Size.Small")]
        public void TryParse_WhenOneshotBootstrapArgumentsExist_ReturnsOneshotPayload ()
        {
            var args = CreateOneshotBootstrapArgs();

            var result = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out _);

            Assert.That(result, Is.True);
            Assert.That(bootstrapArguments, Is.TypeOf<IpcOneshotBootstrapArguments>());
        }

        [Test]
        [Category("Size.Small")]
        public void TryParse_WhenTargetIsMissing_ReturnsFalse ()
        {
            var result = IpcBatchmodeBootstrapArgumentsCodec.TryParse(
                new[]
                {
                    "Unity",
                    "-batchmode",
                },
                out _,
                out _);

            Assert.That(result, Is.False);
        }

        private static IReadOnlyList<string> CreateDaemonBootstrapArgs ()
        {
            var args = new List<string>
            {
                "Unity",
                "-batchmode",
            };
            IpcBatchmodeBootstrapArgumentsCodec.AppendTokens(
                args,
                new IpcDaemonBootstrapArguments(
                    RepositoryRoot: "/repo",
                    ProjectFingerprint: "fingerprint",
                    SessionPath: "/repo/.ucli/session.json",
                    EndpointTransportKind: "unixDomainSocket",
                    EndpointAddress: "/tmp/ucli.sock"));
            return args;
        }

        private static IReadOnlyList<string> CreateOneshotBootstrapArgs ()
        {
            var args = new List<string>
            {
                "Unity",
                "-batchmode",
            };
            IpcBatchmodeBootstrapArgumentsCodec.AppendTokens(
                args,
                new IpcOneshotBootstrapArguments(
                    "/tmp/request.json",
                    "/tmp/response.json"));
            return args;
        }
    }
}
