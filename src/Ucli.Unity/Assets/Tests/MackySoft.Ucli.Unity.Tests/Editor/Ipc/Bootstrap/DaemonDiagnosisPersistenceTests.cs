using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
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
            var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
            var bootstrapArguments = new IpcDaemonBootstrapArguments(
                RepositoryRoot: storageRoot,
                ProjectFingerprint: projectFingerprint,
                SessionPath: UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint),
                SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
                EndpointTransportKind: "unixDomainSocket",
                EndpointAddress: "/tmp/ucli.sock");

            try
            {
                DaemonDiagnosisPersistence.WriteAsync(
                        bootstrapArguments,
                        DaemonDiagnosisReasonValues.ListenerTerminated,
                        "listener terminated",
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(storageRoot, projectFingerprint);
                Assert.That(File.Exists(diagnosisPath), Is.True);

                var json = File.ReadAllText(diagnosisPath);
                var contract = DaemonDiagnosisJsonContractSerializer.Deserialize(json);
                Assert.That(contract, Is.Not.Null);
                Assert.That(contract!.Reason, Is.EqualTo(DaemonDiagnosisReasonValues.ListenerTerminated));
                Assert.That(contract.Message, Is.EqualTo("listener terminated"));
                Assert.That(contract.ReportedBy, Is.EqualTo(DaemonDiagnosisReportedByValues.Unity));
                Assert.That(contract.IsInferred, Is.False);
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
