using System;
using System.IO;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Captures the pre-reload recovery lease before a successor lifecycle sidecar replaces the observation.
    /// </summary>
    internal static class UnityLifecycleExecutionRecoveryLeaseReader
    {
        public static DaemonLifecycleRecoveryLease TryRead (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            DateTimeOffset nowUtc)
        {
            if (storageRoot == null)
            {
                throw new ArgumentNullException(nameof(storageRoot));
            }

            if (projectFingerprint == null)
            {
                throw new ArgumentNullException(nameof(projectFingerprint));
            }

            if (nowUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Current time must use the UTC offset.",
                    nameof(nowUtc));
            }

            try
            {
                var contents = FileUtilities.ReadAllTextOrNull(
                    UcliStoragePathResolver.ResolveDaemonLifecyclePath(
                        storageRoot,
                        projectFingerprint));
                if (contents == null)
                {
                    return null;
                }

                var contract = DaemonLifecycleJsonContractSerializer.Deserialize(contents);
                var lease = contract?.RecoveryLease;
                return lease != null && lease.ExpiresAtUtc > nowUtc
                    ? lease
                    : null;
            }
            catch (Exception exception) when (
                exception is IOException
                    or JsonException
                    or ArgumentException)
            {
                return null;
            }
        }
    }
}
