using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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

                using var document = JsonDocument.Parse(File.ReadAllText(diagnosisPath.Value));
                var root = document.RootElement;
                Assert.That(root.GetProperty("reason").GetString(), Is.EqualTo("listenerTerminated"));
                Assert.That(root.GetProperty("message").GetString(), Is.EqualTo("listener terminated"));
                Assert.That(root.GetProperty("reportedBy").GetString(), Is.EqualTo("unity"));
                Assert.That(root.GetProperty("isInferred").GetBoolean(), Is.False);
                Assert.That(root.GetProperty("processId").GetInt32(), Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(
                    root.GetProperty("sessionIssuedAtUtc").GetDateTimeOffset(),
                    Is.EqualTo(bootstrapContext.SessionIssuedAtUtc));
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
