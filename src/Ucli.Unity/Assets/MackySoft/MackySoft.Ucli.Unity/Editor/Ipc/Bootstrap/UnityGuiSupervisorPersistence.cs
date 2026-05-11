using System;
using System.Diagnostics;
using System.IO;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists GUI supervisor metadata for CLI-side rebootstrap attach. </summary>
    internal static class UnityGuiSupervisorPersistence
    {
        public static GuiSupervisorManifestJsonContract Write (
            string storageRoot,
            string projectFingerprint,
            IpcEndpoint endpoint,
            string sessionToken,
            DateTimeOffset issuedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
            }

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
            }

            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                throw new ArgumentException("Session token must not be empty.", nameof(sessionToken));
            }

            var manifest = new GuiSupervisorManifestJsonContract(
                SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
                SessionToken: sessionToken,
                ProjectFingerprint: projectFingerprint,
                EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
                EndpointAddress: endpoint.Address,
                ProcessId: Process.GetCurrentProcess().Id,
                ProcessStartedAtUtc: TryGetCurrentProcessStartedAtUtc(),
                IssuedAtUtc: issuedAtUtc);
            var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(storageRoot, projectFingerprint);
            var manifestDirectory = Path.GetDirectoryName(manifestPath);
            if (string.IsNullOrEmpty(manifestDirectory))
            {
                throw new InvalidOperationException($"GUI supervisor manifest directory could not be resolved. Path={manifestPath}");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(manifestDirectory);
            File.WriteAllText(
                manifestPath,
                GuiSupervisorManifestJsonContractSerializer.Serialize(manifest) + Environment.NewLine);
            FileSystemAccessBoundary.EnsureSecureFile(manifestPath);
            return manifest;
        }

        public static void Delete (
            string storageRoot,
            string projectFingerprint)
        {
            var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(storageRoot, projectFingerprint);
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }

        private static DateTimeOffset? TryGetCurrentProcessStartedAtUtc ()
        {
            try
            {
                return new DateTimeOffset(Process.GetCurrentProcess().StartTime).ToUniversalTime();
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
            {
                return null;
            }
        }
    }
}
