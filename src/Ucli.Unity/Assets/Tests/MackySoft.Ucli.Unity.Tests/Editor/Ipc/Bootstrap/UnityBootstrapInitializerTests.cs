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
            Assert.That(
                ((IpcOneshotBootstrapArguments)bootstrapArguments).BootstrapId,
                Is.EqualTo(System.Guid.Parse("386052a2-f938-414b-930b-47b687844237")));
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
        public void HasOneshotTarget_WhenOneshotPayloadIsOtherwiseInvalid_ReturnsTrue ()
        {
            var result = UnityBootstrapInitializer.HasOneshotTarget(
                new[]
                {
                    "Unity",
                    IpcBatchmodeBootstrapArgumentNames.Target,
                    "oneshot",
                    IpcOneshotBootstrapArgumentNames.BootstrapId,
                    "invalid",
                });

            Assert.That(result, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void HasOneshotTarget_WhenTargetIsDifferent_ReturnsFalse ()
        {
            var result = UnityBootstrapInitializer.HasOneshotTarget(
                new[]
                {
                    "Unity",
                    IpcBatchmodeBootstrapArgumentNames.Target,
                    "daemon",
                });

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
                    "daemon",
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
                    "daemon",
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

        private static string[] CreateDaemonBootstrapArgs ()
        {
            return new[]
            {
                "Unity",
                "-batchmode",
                "-ucliBootstrapTarget",
                "daemon",
                "-ucliRepositoryRoot",
                "/repo",
                "-ucliProjectFingerprint",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "-ucliSessionPath",
                "/repo/.ucli/session.json",
                "-ucliSessionGenerationId",
                "11111111-1111-1111-1111-111111111111",
                "-ucliSessionIssuedAtUtc",
                "2026-03-09T00:00:00.0000000+00:00",
                "-ucliEndpointTransportKind",
                "unixDomainSocket",
                "-ucliEndpointAddress",
                "/tmp/ucli.sock",
            };
        }

        private static string[] CreateOneshotBootstrapArgs ()
        {
            return new[]
            {
                "Unity",
                "-batchmode",
                "-ucliBootstrapTarget",
                "oneshot",
                "-ucliOneshotBootstrapId",
                "386052a2-f938-414b-930b-47b687844237",
            };
        }
    }
}
