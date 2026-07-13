using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityBootstrapInitializerTests
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
            Assert.That(
                ((IpcOneshotBootstrapArguments)bootstrapArguments).ProjectFingerprint,
                Is.EqualTo(ProjectFingerprintTestFactory.Create("project-fingerprint")));
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

        [Test]
        [Category("Size.Small")]
        public void TryResolveGuiBootstrapArguments_WhenTargetIsMissing_ReturnsUserOwnedBootstrap ()
        {
            var resolved = UnityBootstrapInitializer.TryResolveGuiBootstrapArguments(
                new[] { "Unity" },
                out var arguments,
                out var error);

            Assert.That(resolved, Is.True);
            Assert.That(arguments, Is.Null);
            Assert.That(error, Is.EqualTo(IpcGuiBootstrapParseError.None));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolveGuiBootstrapArguments_WhenCliMarkerIsValid_ReturnsCliBootstrapArguments ()
        {
            var resolved = UnityBootstrapInitializer.TryResolveGuiBootstrapArguments(
                new[]
                {
                    "Unity",
                    IpcGuiBootstrapArgumentNames.Target,
                    IpcGuiBootstrapTargetValues.Daemon,
                    IpcGuiBootstrapArgumentNames.OwnerProcessId,
                    "123",
                    IpcGuiBootstrapArgumentNames.CanShutdownProcess,
                    "false",
                },
                out var arguments,
                out var error);

            Assert.That(resolved, Is.True);
            Assert.That(error, Is.EqualTo(IpcGuiBootstrapParseError.None));
            Assert.That(arguments.OwnerProcessId, Is.EqualTo(123));
            Assert.That(arguments.CanShutdownProcess, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolveGuiBootstrapArguments_WhenCliMarkerIsInvalid_ReturnsInvalid ()
        {
            var resolved = UnityBootstrapInitializer.TryResolveGuiBootstrapArguments(
                new[]
                {
                    "Unity",
                    IpcGuiBootstrapArgumentNames.Target,
                    IpcGuiBootstrapTargetValues.Daemon,
                    IpcGuiBootstrapArgumentNames.OwnerProcessId,
                    "0",
                    IpcGuiBootstrapArgumentNames.CanShutdownProcess,
                    "false",
                },
                out _,
                out var error);

            Assert.That(resolved, Is.False);
            Assert.That(error.Kind, Is.EqualTo(IpcGuiBootstrapParseErrorKind.InvalidRequiredValue));
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
                    ProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
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
                    ProjectFingerprintTestFactory.Create("project-fingerprint"),
                    "oneshot-token",
                    new System.DateTimeOffset(2026, 03, 09, 0, 0, 0, System.TimeSpan.Zero),
                    "unixDomainSocket",
                    "/tmp/ucli.sock"));
            return args;
        }
    }
}
