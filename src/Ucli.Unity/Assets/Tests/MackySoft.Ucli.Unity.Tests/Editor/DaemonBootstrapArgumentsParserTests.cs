using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DaemonBootstrapArgumentsParserTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryParse_WhenValueIsMissingAndNextTokenIsArgumentName_ReturnsFalse ()
        {
            var parser = new DaemonBootstrapArgumentsParser();
            var args = new[]
            {
                "-batchmode",
                IpcDaemonBootstrapArgumentNames.RepositoryRoot,
                IpcDaemonBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
                IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
                IpcDaemonBootstrapArgumentNames.EndpointTransportKind, "namedPipe",
                IpcDaemonBootstrapArgumentNames.EndpointAddress, "ucli-endpoint",
            };

            var parsed = parser.TryParse(args, out _, out var errorMessage);

            Assert.That(parsed, Is.False);
            Assert.That(errorMessage, Is.EqualTo("uCLI daemon bootstrap arguments are missing."));
        }

        [Test]
        [Category("Size.Small")]
        public void TryParse_WhenValueStartsWithHyphenButIsNotArgumentName_ParsesSuccessfully ()
        {
            var parser = new DaemonBootstrapArgumentsParser();
            var args = new[]
            {
                IpcDaemonBootstrapArgumentNames.RepositoryRoot, "-tmp-repository",
                IpcDaemonBootstrapArgumentNames.ProjectFingerprint, "fingerprint",
                IpcDaemonBootstrapArgumentNames.SessionPath, "/tmp/session.json",
                IpcDaemonBootstrapArgumentNames.EndpointTransportKind, "namedPipe",
                IpcDaemonBootstrapArgumentNames.EndpointAddress, "ucli-endpoint",
            };

            var parsed = parser.TryParse(args, out var bootstrapArguments, out var errorMessage);

            Assert.That(parsed, Is.True);
            Assert.That(errorMessage, Is.EqualTo(string.Empty));
            Assert.That(bootstrapArguments.RepositoryRoot, Is.EqualTo("-tmp-repository"));
        }
    }
}
