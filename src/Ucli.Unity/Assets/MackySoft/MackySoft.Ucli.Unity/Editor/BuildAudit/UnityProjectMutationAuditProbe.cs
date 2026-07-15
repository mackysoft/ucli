using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
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
            return CaptureObservation(projectPath);
        }

        /// <summary> Compares a baseline snapshot with the current project state. </summary>
        /// <param name="projectPath"> The Unity project root path. </param>
        /// <param name="mode"> The project mutation policy mode literal. </param>
        /// <param name="baseline"> The previously captured baseline. </param>
        /// <returns> The completed project mutation audit. </returns>
        public IpcBuildProjectMutationAudit Complete (
            string projectPath,
            BuildProfileProjectMutationMode mode,
            ProjectMutationSnapshot baseline)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(nameof(baseline));
            }

            var after = CaptureObservation(projectPath);
            var items = CreateItems(baseline.FilesByPath, after.FilesByPath);
            var coverage = ResolveCoverage(baseline.Coverage, after.Coverage);
            return new IpcBuildProjectMutationAudit(
                Mode: mode,
                Coverage: coverage,
                Mutated: items.Count != 0,
                BeforeDigest: baseline.Digest,
                AfterDigest: after.Digest,
                Items: items);
        }

        private static ProjectMutationSnapshot CaptureObservation (string projectPath)
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
            var auditedRootRelativePaths = ProjectMutationAuditPath.RootDirectoryNames;
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

            files.Sort(static (left, right) => left.Path.CompareTo(right.Path));
            var filesByPath = new Dictionary<ProjectMutationAuditPath, ProjectMutationFileEntry>(files.Count);
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
                    var relativePath = CreateProjectMutationAuditPath(projectRoot, fullPath);
                    files.Add(new ProjectMutationFileEntry(relativePath, ComputeFileSha256(fullPath)));
                }
            }

            return fullCoverage;
        }

        private static IReadOnlyList<IpcBuildProjectMutationAuditItem> CreateItems (
            IReadOnlyDictionary<ProjectMutationAuditPath, ProjectMutationFileEntry> before,
            IReadOnlyDictionary<ProjectMutationAuditPath, ProjectMutationFileEntry> after)
        {
            var paths = new SortedSet<ProjectMutationAuditPath>();
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
                        IpcBuildProjectMutationChangeKind.Added,
                        BeforeSha256: null,
                        AfterSha256: afterEntry!.Sha256));
                    continue;
                }

                if (hadBefore && !hasAfter)
                {
                    items.Add(new IpcBuildProjectMutationAuditItem(
                        path,
                        IpcBuildProjectMutationChangeKind.Deleted,
                        beforeEntry!.Sha256,
                        AfterSha256: null));
                    continue;
                }

                if (hadBefore
                    && hasAfter
                    && beforeEntry!.Sha256 != afterEntry!.Sha256)
                {
                    items.Add(new IpcBuildProjectMutationAuditItem(
                        path,
                        IpcBuildProjectMutationChangeKind.Modified,
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

        private static ProjectMutationAuditPath CreateProjectMutationAuditPath (
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

        private static Sha256Digest CalculateAggregateDigest (IReadOnlyList<ProjectMutationFileEntry> files)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < files.Count; i++)
            {
                builder.Append(files[i].Path);
                builder.Append('\0');
                builder.Append(files[i].Sha256);
                builder.Append('\n');
            }

            return Sha256Digest.Compute(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        private static Sha256Digest ComputeFileSha256 (string path)
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
                return Sha256Digest.FromHashBytes(sha256.ComputeHash(stream));
            }
        }

        internal sealed record ProjectMutationSnapshot (
            IpcBuildProjectMutationAuditCoverage Coverage,
            Sha256Digest Digest,
            IReadOnlyDictionary<ProjectMutationAuditPath, ProjectMutationFileEntry> FilesByPath);

        internal sealed record ProjectMutationFileEntry (
            ProjectMutationAuditPath Path,
            Sha256Digest Sha256);
    }
}
