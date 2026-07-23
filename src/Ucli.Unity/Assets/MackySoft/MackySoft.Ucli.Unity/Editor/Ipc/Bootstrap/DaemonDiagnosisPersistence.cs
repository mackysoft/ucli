using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists daemon termination diagnosis files for one fingerprint-scoped daemon lifecycle. </summary>
    internal static class DaemonDiagnosisPersistence
    {
        /// <summary> Writes one daemon diagnosis snapshot to fingerprint-scoped shared storage. </summary>
        /// <param name="bootstrapContext"> The guarded daemon bootstrap context that defines storage scope. </param>
        /// <param name="reason"> The normalized daemon diagnosis reason. </param>
        /// <param name="message"> The human-readable daemon diagnosis message. </param>
        internal static async Task WriteAsync (
            UnityDaemonBootstrapContext bootstrapContext,
            DaemonDiagnosisReason reason,
            string message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (bootstrapContext == null)
            {
                throw new ArgumentNullException(nameof(bootstrapContext));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("message must not be empty.", nameof(message));
            }

            var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(
                bootstrapContext.RepositoryRoot,
                bootstrapContext.ProjectFingerprint);
            using var currentProcess = Process.GetCurrentProcess();
            var diagnosisContract = new DaemonDiagnosisJsonContract(
                Reason: reason,
                Message: message,
                ReportedBy: DaemonDiagnosisReportedBy.Unity,
                IsInferred: false,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                ProcessId: currentProcess.Id,
                EditorInstancePath: null,
                SessionIssuedAtUtc: bootstrapContext.SessionIssuedAtUtc,
                ProcessStartedAtUtc: new DateTimeOffset(currentProcess.StartTime.ToUniversalTime()),
                UnityLogPath: null,
                StartupPhase: null,
                ActionRequired: null,
                PrimaryDiagnostic: null);
            var json = DaemonDiagnosisJsonContractSerializer.Serialize(diagnosisContract) + Environment.NewLine;
            if (!diagnosisPath.TryGetParent(out var diagnosisDirectoryPath))
            {
                throw new InvalidOperationException(
                    $"Daemon diagnosis directory path could not be resolved: {diagnosisPath}");
            }
            UcliLocalStorageBootstrapper.EnsureInitialized(diagnosisDirectoryPath);
            Directory.CreateDirectory(diagnosisDirectoryPath.Value);
            await FileUtilities.WriteAllTextAtomicallyAsync(diagnosisPath, json, cancellationToken).ConfigureAwait(false);
        }
    }
}
