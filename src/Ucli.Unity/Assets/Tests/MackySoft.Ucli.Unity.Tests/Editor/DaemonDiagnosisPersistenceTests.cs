using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DaemonDiagnosisPersistenceTests
    {
        [Test]
        [Category("Size.Small")]
        public void Write_PersistsDiagnosisJson ()
        {
            var storageRoot = Path.Combine(Path.GetTempPath(), $"ucli-daemon-diagnosis-tests-{Guid.NewGuid():N}");
            var bootstrapArguments = new IpcDaemonBootstrapArguments(
                RepositoryRoot: storageRoot,
                ProjectFingerprint: "fingerprint",
                SessionPath: Path.Combine(storageRoot, ".ucli", "local", "fingerprints", "fingerprint", "session.json"),
                SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
                EndpointTransportKind: IpcTransportKindValues.UnixDomainSocket,
                EndpointAddress: "/tmp/ucli.sock");

            try
            {
                DaemonDiagnosisPersistence.Write(
                        bootstrapArguments,
                        DaemonDiagnosisReasonValues.ListenerTerminated,
                        "listener terminated",
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, "fingerprint");
                Assert.That(File.Exists(diagnosisPath), Is.True);

                var json = File.ReadAllText(diagnosisPath);
                var contract = DaemonDiagnosisJsonContractSerializer.Deserialize(json);
                Assert.That(contract, Is.Not.Null);
                Assert.That(contract!.Reason, Is.EqualTo(DaemonDiagnosisReasonValues.ListenerTerminated));
                Assert.That(contract.Message, Is.EqualTo("listener terminated"));
                Assert.That(contract.ProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(contract.SessionIssuedAtUtc, Is.EqualTo(bootstrapArguments.SessionIssuedAtUtc));
            }
            finally
            {
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, recursive: true);
                }
            }
        }
    }
}
