using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiBootstrapInitializerTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryResolveBootstrapArguments_WhenTargetIsMissing_ReturnsUserOwnedBootstrap ()
        {
            var resolved = UnityGuiBootstrapInitializer.TryResolveBootstrapArguments(
                new[] { "Unity" },
                out var arguments,
                out var error);

            Assert.That(resolved, Is.True);
            Assert.That(arguments, Is.Null);
            Assert.That(error, Is.EqualTo(IpcGuiBootstrapParseError.None));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolveBootstrapArguments_WhenCliMarkerIsValid_ReturnsCliBootstrapArguments ()
        {
            var resolved = UnityGuiBootstrapInitializer.TryResolveBootstrapArguments(
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
        public void TryResolveBootstrapArguments_WhenCliMarkerIsInvalid_ReturnsInvalid ()
        {
            var resolved = UnityGuiBootstrapInitializer.TryResolveBootstrapArguments(
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
    }
}
