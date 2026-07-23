using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MackySoft.FileSystem;
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
            var guardedStorageRoot = AbsolutePath.Parse(storageRoot);
            var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
            var bootstrapContext = new UnityDaemonBootstrapContext(
                guardedStorageRoot,
                projectFingerprint,
                UcliStoragePathResolver.ResolveSessionPath(
                    guardedStorageRoot,
                    projectFingerprint),
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
                UnityIpcEndpointBinding.Create(
                    new IpcEndpoint(
                        IpcTransportKind.NamedPipe,
                        "ucli-daemon-diagnosis-tests")));

            try
            {
                DaemonDiagnosisPersistence.WriteAsync(
                        bootstrapContext,
                        DaemonDiagnosisReason.ListenerTerminated,
                        "listener terminated",
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(
                    guardedStorageRoot,
                    projectFingerprint);
                Assert.That(File.Exists(diagnosisPath.Value), Is.True);

                var json = File.ReadAllText(diagnosisPath.Value);
                var contract = DaemonDiagnosisJsonContractSerializer.Deserialize(json);
                Assert.That(contract, Is.Not.Null);
                Assert.That(contract!.Reason, Is.EqualTo(DaemonDiagnosisReason.ListenerTerminated));
                Assert.That(contract.Message, Is.EqualTo("listener terminated"));
                Assert.That(contract.ReportedBy, Is.EqualTo(DaemonDiagnosisReportedBy.Unity));
                Assert.That(contract.IsInferred, Is.False);
                Assert.That(contract.ProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(contract.SessionIssuedAtUtc, Is.EqualTo(bootstrapContext.SessionIssuedAtUtc));
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
