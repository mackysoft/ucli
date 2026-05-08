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
            Assert.That(((IpcDaemonBootstrapArguments)bootstrapArguments).SessionIssuedAtUtc, Is.EqualTo(new System.DateTimeOffset(2026, 03, 09, 0, 0, 0, System.TimeSpan.Zero)));
        }

        [Test]
        [Category("Size.Small")]
        public void TryParse_WhenOneshotBootstrapArgumentsExist_ReturnsOneshotPayload ()
        {
            var args = CreateOneshotBootstrapArgs();

            var result = IpcBatchmodeBootstrapArgumentsCodec.TryParse(args, out var bootstrapArguments, out _);

            Assert.That(result, Is.True);
            Assert.That(bootstrapArguments, Is.TypeOf<IpcOneshotBootstrapArguments>());
            Assert.That(((IpcOneshotBootstrapArguments)bootstrapArguments).ParentProcessId, Is.EqualTo(123));
            Assert.That(((IpcOneshotBootstrapArguments)bootstrapArguments).ProjectFingerprint, Is.EqualTo("project-fingerprint"));
            Assert.That(((IpcOneshotBootstrapArguments)bootstrapArguments).SessionToken, Is.EqualTo("oneshot-token"));
            Assert.That(((IpcOneshotBootstrapArguments)bootstrapArguments).ExitDeadlineUtc, Is.EqualTo(new System.DateTimeOffset(2026, 03, 09, 0, 0, 0, System.TimeSpan.Zero)));
            Assert.That(((IpcOneshotBootstrapArguments)bootstrapArguments).EndpointTransportKind, Is.EqualTo("unixDomainSocket"));
            Assert.That(((IpcOneshotBootstrapArguments)bootstrapArguments).EndpointAddress, Is.EqualTo("/tmp/ucli.sock"));
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
                    SessionIssuedAtUtc: new System.DateTimeOffset(2026, 03, 09, 0, 0, 0, System.TimeSpan.Zero),
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
                    123,
                    "project-fingerprint",
                    "oneshot-token",
                    new System.DateTimeOffset(2026, 03, 09, 0, 0, 0, System.TimeSpan.Zero),
                    "unixDomainSocket",
                    "/tmp/ucli.sock"));
            return args;
        }
    }
}
