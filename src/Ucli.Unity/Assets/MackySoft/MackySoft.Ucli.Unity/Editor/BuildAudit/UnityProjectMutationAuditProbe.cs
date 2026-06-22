using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Captures and compares project file digests around build runner invocation. </summary>
    internal sealed class UnityProjectMutationAuditProbe
    {
        private const int FileStreamBufferSize = 81920;

        /// <summary> Captures the current project mutation audit baseline. </summary>
        /// <param name="projectPath"> The Unity project root path. </param>
        /// <returns> The captured project snapshot. </returns>
        public ProjectMutationSnapshot CaptureBaseline (string projectPath)
        {
            return CaptureSnapshot(projectPath);
        }

        /// <summary> Compares a baseline snapshot with the current project state. </summary>
        /// <param name="projectPath"> The Unity project root path. </param>
        /// <param name="mode"> The project mutation policy mode literal. </param>
        /// <param name="baseline"> The previously captured baseline. </param>
        /// <returns> The completed project mutation audit. </returns>
        public IpcBuildProjectMutationAudit Complete (
            string projectPath,
            string mode,
            ProjectMutationSnapshot baseline)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(nameof(baseline));
            }

            var after = CaptureSnapshot(projectPath);
            var items = CreateItems(baseline.FilesByPath, after.FilesByPath);
            var coverage = ResolveCoverage(baseline.Coverage, after.Coverage);
            return new IpcBuildProjectMutationAudit(
                Mode: mode,
                Coverage: ContractLiteralCodec.ToValue(coverage),
                Mutated: items.Count != 0,
                BeforeDigest: baseline.Digest,
                AfterDigest: after.Digest,
                Items: items);
        }

        private static ProjectMutationSnapshot CaptureSnapshot (string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException("Project path must not be empty.", nameof(projectPath));
            }

            var projectPathResult = PathNormalizer.TryNormalizeFullPath(projectPath);
            if (!projectPathResult.IsSuccess)
            {
                throw new ArgumentException(projectPathResult.DiagnosticMessage, nameof(projectPath));
            }

            var projectRoot = projectPathResult.FullPath!;
            var files = new List<ProjectMutationFileEntry>();
            var coverage = IpcBuildProjectMutationAuditCoverage.Full;
            var scannedRootCount = 0;
            var auditedRootRelativePaths = UnityProjectMutationAuditScope.RootRelativePaths;
            for (var i = 0; i < auditedRootRelativePaths.Count; i++)
            {
                var rootRelativePath = auditedRootRelativePaths[i];
                var rootPath = Path.Combine(projectRoot, rootRelativePath);
                if (!Directory.Exists(rootPath))
                {
                    coverage = IpcBuildProjectMutationAuditCoverage.Partial;
                    continue;
                }

                try
                {
                    if (!CaptureRoot(projectRoot, rootPath, files))
                    {
                        coverage = IpcBuildProjectMutationAuditCoverage.Partial;
                    }

                    scannedRootCount++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    coverage = IpcBuildProjectMutationAuditCoverage.Partial;
                }
            }

            if (scannedRootCount == 0)
            {
                coverage = IpcBuildProjectMutationAuditCoverage.Indeterminate;
            }

            files.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
            var filesByPath = new Dictionary<string, ProjectMutationFileEntry>(files.Count, StringComparer.Ordinal);
            for (var i = 0; i < files.Count; i++)
            {
                filesByPath[files[i].Path] = files[i];
            }

            return new ProjectMutationSnapshot(
                coverage,
                CalculateAggregateDigest(files),
                filesByPath);
        }

        private static bool CaptureRoot (
            string projectRoot,
            string rootPath,
            List<ProjectMutationFileEntry> files)
        {
            var fullCoverage = true;
            var pendingDirectories = new Stack<string>();
            var normalizedRootPath = Path.GetFullPath(rootPath);
            var rootAttributes = File.GetAttributes(normalizedRootPath);
            if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            pendingDirectories.Push(normalizedRootPath);
            while (pendingDirectories.Count > 0)
            {
                var currentDirectory = pendingDirectories.Pop();
                foreach (var entryPath in Directory.EnumerateFileSystemEntries(currentDirectory))
                {
                    var attributes = File.GetAttributes(entryPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        fullCoverage = false;
                        continue;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pendingDirectories.Push(Path.GetFullPath(entryPath));
                        continue;
                    }

                    var fullPath = Path.GetFullPath(entryPath);
                    var relativePath = NormalizeProjectRelativePath(projectRoot, fullPath);
                    files.Add(new ProjectMutationFileEntry(relativePath, ComputeFileSha256(fullPath)));
                }
            }

            return fullCoverage;
        }

        private static IReadOnlyList<IpcBuildProjectMutationAuditItem> CreateItems (
            IReadOnlyDictionary<string, ProjectMutationFileEntry> before,
            IReadOnlyDictionary<string, ProjectMutationFileEntry> after)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var path in before.Keys)
            {
                paths.Add(path);
            }

            foreach (var path in after.Keys)
            {
                paths.Add(path);
            }

            var items = new List<IpcBuildProjectMutationAuditItem>();
            foreach (var path in paths)
            {
                var hadBefore = before.TryGetValue(path, out var beforeEntry);
                var hasAfter = after.TryGetValue(path, out var afterEntry);
                if (!hadBefore && hasAfter)
                {
                    items.Add(new IpcBuildProjectMutationAuditItem(
                        path,
                        ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Added),
                        BeforeSha256: null,
                        AfterSha256: afterEntry!.Sha256));
                    continue;
                }

                if (hadBefore && !hasAfter)
                {
                    items.Add(new IpcBuildProjectMutationAuditItem(
                        path,
                        ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Deleted),
                        beforeEntry!.Sha256,
                        AfterSha256: null));
                    continue;
                }

                if (hadBefore
                    && hasAfter
                    && !string.Equals(beforeEntry!.Sha256, afterEntry!.Sha256, StringComparison.Ordinal))
                {
                    items.Add(new IpcBuildProjectMutationAuditItem(
                        path,
                        ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Modified),
                        beforeEntry.Sha256,
                        afterEntry.Sha256));
                }
            }

            return items;
        }

        private static IpcBuildProjectMutationAuditCoverage ResolveCoverage (
            IpcBuildProjectMutationAuditCoverage baselineCoverage,
            IpcBuildProjectMutationAuditCoverage afterCoverage)
        {
            if (baselineCoverage == IpcBuildProjectMutationAuditCoverage.Indeterminate
                || afterCoverage == IpcBuildProjectMutationAuditCoverage.Indeterminate)
            {
                return IpcBuildProjectMutationAuditCoverage.Indeterminate;
            }

            if (baselineCoverage == IpcBuildProjectMutationAuditCoverage.Partial
                || afterCoverage == IpcBuildProjectMutationAuditCoverage.Partial)
            {
                return IpcBuildProjectMutationAuditCoverage.Partial;
            }

            return IpcBuildProjectMutationAuditCoverage.Full;
        }

        private static string NormalizeProjectRelativePath (
            string projectRoot,
            string fullPath)
        {
            var result = RepositoryPathNormalizer.TryNormalize(projectRoot, fullPath);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.DiagnosticMessage);
            }

            return result.RepositoryRelativeSlashPath!;
        }

        private static string CalculateAggregateDigest (IReadOnlyList<ProjectMutationFileEntry> files)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < files.Count; i++)
            {
                builder.Append(files[i].Path);
                builder.Append('\0');
                builder.Append(files[i].Sha256);
                builder.Append('\n');
            }

            using (var sha256 = SHA256.Create())
            {
                return ToLowerHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
            }
        }

        private static string ComputeFileSha256 (string path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite,
                       FileStreamBufferSize,
                       FileOptions.SequentialScan))
            {
                return ToLowerHex(sha256.ComputeHash(stream));
            }
        }

        private static string ToLowerHex (byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        internal sealed record ProjectMutationSnapshot (
            IpcBuildProjectMutationAuditCoverage Coverage,
            string Digest,
            IReadOnlyDictionary<string, ProjectMutationFileEntry> FilesByPath);

        internal sealed record ProjectMutationFileEntry (
            string Path,
            string Sha256);
    }
}
