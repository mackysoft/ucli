using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists daemon termination diagnosis files for one fingerprint-scoped daemon lifecycle. </summary>
    internal static class DaemonDiagnosisPersistence
    {
        /// <summary> Writes one daemon diagnosis snapshot to fingerprint-scoped shared storage. </summary>
        /// <param name="bootstrapArguments"> The daemon bootstrap arguments that define storage scope. </param>
        /// <param name="reason"> The normalized daemon diagnosis reason. </param>
        /// <param name="message"> The human-readable daemon diagnosis message. </param>
        internal static async Task Write (
            IpcDaemonBootstrapArguments bootstrapArguments,
            string reason,
            string message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("reason must not be empty.", nameof(reason));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("message must not be empty.", nameof(message));
            }

            var diagnosisPath = UcliStoragePathResolver.ResolveDaemonDiagnosisPath(
                bootstrapArguments.RepositoryRoot,
                bootstrapArguments.ProjectFingerprint);
            var diagnosisContract = new DaemonDiagnosisJsonContract(
                Reason: reason,
                Message: message,
                ReportedBy: DaemonDiagnosisReportedByValues.Unity,
                IsInferred: false,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                ProcessId: Process.GetCurrentProcess().Id,
                SessionIssuedAtUtc: bootstrapArguments.SessionIssuedAtUtc);
            var json = DaemonDiagnosisJsonContractSerializer.Serialize(diagnosisContract) + Environment.NewLine;
            await FileUtilities.WriteAllTextAtomically(diagnosisPath, json, cancellationToken).ConfigureAwait(false);
        }
    }
}