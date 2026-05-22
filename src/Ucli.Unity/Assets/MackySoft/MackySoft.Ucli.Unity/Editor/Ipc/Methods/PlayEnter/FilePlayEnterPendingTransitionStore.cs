using System;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Stores one pending Play Mode enter transition in the project-local uCLI state directory. </summary>
    internal sealed class FilePlayEnterPendingTransitionStore : IPlayEnterPendingTransitionStore
    {
        private const int SchemaVersion = 1;
        private const string FileName = "play-enter-pending.json";

        private readonly string path;
        private readonly string projectFingerprint;

        private FilePlayEnterPendingTransitionStore (
            string path,
            string projectFingerprint)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be empty.", nameof(path));
            }

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
            }

            this.path = path;
            this.projectFingerprint = projectFingerprint;
        }

        /// <summary> Creates a file-backed store for the Unity project served by the IPC host. </summary>
        public static FilePlayEnterPendingTransitionStore Create (IpcProjectIdentity projectIdentity)
        {
            if (projectIdentity == null)
            {
                throw new ArgumentNullException(nameof(projectIdentity));
            }

            var storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectIdentity.ProjectPath);
            var fingerprintDirectory = UcliStoragePathResolver.ResolveFingerprintDirectory(
                storageRoot,
                projectIdentity.ProjectFingerprint);
            return new FilePlayEnterPendingTransitionStore(
                Path.Combine(fingerprintDirectory, FileName),
                projectIdentity.ProjectFingerprint);
        }

        /// <inheritdoc />
        public bool TryWrite (
            IpcPlayLifecycleSnapshot before,
            out string errorMessage)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var document = new PendingTransitionDocument
                {
                    SchemaVersion = SchemaVersion,
                    ProjectFingerprint = projectFingerprint,
                    Before = before,
                };
                var json = JsonSerializer.Serialize(document, IpcJsonSerializerOptions.Default);
                var temporaryPath = string.Concat(path, ".tmp");
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporaryPath, path);
                errorMessage = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryRead (
            out IpcPlayLifecycleSnapshot before,
            out string errorMessage)
        {
            before = null;
            if (!File.Exists(path))
            {
                errorMessage = null;
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                var document = JsonSerializer.Deserialize<PendingTransitionDocument>(
                    json,
                    IpcJsonSerializerOptions.Default);
                if (document == null
                    || document.SchemaVersion != SchemaVersion
                    || !string.Equals(document.ProjectFingerprint, projectFingerprint, StringComparison.Ordinal)
                    || document.Before == null)
                {
                    errorMessage = "Pending transition document is invalid.";
                    return false;
                }

                before = document.Before;
                errorMessage = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryDelete (out string errorMessage)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                var temporaryPath = string.Concat(path, ".tmp");
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                errorMessage = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        private sealed class PendingTransitionDocument
        {
            public int SchemaVersion { get; set; }

            public string ProjectFingerprint { get; set; }

            public IpcPlayLifecycleSnapshot Before { get; set; }
        }
    }
}
