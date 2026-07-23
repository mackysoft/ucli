using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Cryptography;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Captures and compares project file digests around build runner invocation. </summary>
    internal sealed class UnityProjectMutationAuditProbe
    {
        private const int FileStreamBufferSize = 81920;

        private const int RootCaptureAttemptLimit = 3;

        /// <summary> Captures the current project mutation audit baseline. </summary>
        /// <param name="projectPath"> The Unity project root path. </param>
        /// <returns> The captured project snapshot. </returns>
        public ProjectMutationSnapshot CaptureBaseline (AbsolutePath projectPath)
        {
            return CaptureObservation(projectPath);
        }

        /// <summary> Compares a baseline snapshot with the current project state. </summary>
        /// <param name="projectPath"> The Unity project root path. </param>
        /// <param name="mode"> The project mutation policy mode literal. </param>
        /// <param name="baseline"> The previously captured baseline. </param>
        /// <returns> The completed project mutation audit. </returns>
        public IpcBuildProjectMutationAudit Complete (
            AbsolutePath projectPath,
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

        private static ProjectMutationSnapshot CaptureObservation (AbsolutePath projectPath)
        {
            if (projectPath == null)
            {
                throw new ArgumentNullException(nameof(projectPath));
            }

            var files = new List<ProjectMutationFileEntry>();
            var coverage = IpcBuildProjectMutationAuditCoverage.Full;
            var scannedRootCount = 0;
            var auditedRootRelativePaths = ProjectMutationAuditPath.RootDirectoryNames;
            for (var i = 0; i < auditedRootRelativePaths.Count; i++)
            {
                var rootRelativePath = auditedRootRelativePaths[i];
                var rootPath = ContainedPath.Create(
                    projectPath,
                    RootRelativePath.Parse(rootRelativePath)).Target;
                if (!Directory.Exists(rootPath.Value))
                {
                    coverage = IpcBuildProjectMutationAuditCoverage.Partial;
                    continue;
                }

                var rootFiles = new List<ProjectMutationFileEntry>();
                var rootCaptured = false;
                var rootHasFullCoverage = false;
                for (var attempt = 0; attempt < RootCaptureAttemptLimit; attempt++)
                {
                    rootFiles.Clear();
                    try
                    {
                        rootHasFullCoverage = CaptureRoot(projectPath, rootPath, rootFiles);
                        rootCaptured = true;
                        break;
                    }
                    catch (IOException)
                    {
                        // Unity imports can replace entries while the live project tree is scanned.
                        // Discard the incomplete root observation and retry it as one unit.
                    }
                    catch (Exception exception) when (exception is UnauthorizedAccessException or NotSupportedException)
                    {
                        break;
                    }
                }

                if (!rootCaptured)
                {
                    coverage = IpcBuildProjectMutationAuditCoverage.Partial;
                    continue;
                }

                files.AddRange(rootFiles);
                if (!rootHasFullCoverage)
                {
                    coverage = IpcBuildProjectMutationAuditCoverage.Partial;
                }

                scannedRootCount++;
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
            AbsolutePath projectRoot,
            AbsolutePath rootPath,
            List<ProjectMutationFileEntry> files)
        {
            var fullCoverage = true;
            var pendingDirectories = new Stack<AbsolutePath>();
            var rootAttributes = File.GetAttributes(rootPath.Value);
            if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            pendingDirectories.Push(rootPath);
            while (pendingDirectories.Count > 0)
            {
                var currentDirectory = pendingDirectories.Pop();
                foreach (var entryPathText in Directory.EnumerateFileSystemEntries(currentDirectory.Value))
                {
                    var entryPath = AbsolutePath.Parse(entryPathText);
                    var attributes = File.GetAttributes(entryPath.Value);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        fullCoverage = false;
                        continue;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pendingDirectories.Push(entryPath);
                        continue;
                    }

                    if (!TryCreateProjectMutationAuditPath(projectRoot, entryPath, out var relativePath))
                    {
                        // The portable audit contract cannot represent every filename that is legal
                        // on the current platform, such as a literal backslash on Unix.
                        fullCoverage = false;
                        continue;
                    }

                    files.Add(new ProjectMutationFileEntry(relativePath, ComputeFileSha256(entryPath)));
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

        private static bool TryCreateProjectMutationAuditPath (
            AbsolutePath projectRoot,
            AbsolutePath fullPath,
            out ProjectMutationAuditPath? auditPath)
        {
            return ProjectMutationAuditPathAdapter.TryFromRootRelativePath(
                ContainedPath.Create(projectRoot, fullPath).RelativePath,
                out auditPath);
        }

        private static Sha256Digest CalculateAggregateDigest (IReadOnlyList<ProjectMutationFileEntry> files)
        {
            using var hashWriter = new Utf8Sha256HashWriter();
            for (var i = 0; i < files.Count; i++)
            {
                hashWriter.Append(files[i].Path.Value);
                hashWriter.Append('\0');
                hashWriter.Append(files[i].Sha256.ToString());
                hashWriter.Append('\n');
            }

            return hashWriter.GetHashAndReset();
        }

        private static Sha256Digest ComputeFileSha256 (AbsolutePath path)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(
                       path.Value,
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
