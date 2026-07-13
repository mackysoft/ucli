using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.PlanToken;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Computes state-fingerprint hash values used by plan-token workflows. </summary>
    internal static class PlanTokenStateFingerprintCalculator
    {
        private const string NaLiteral = "na";

        /// <summary> Computes deterministic state fingerprint for token payload validation. </summary>
        /// <param name="snapshot"> The runtime environment snapshot. </param>
        /// <param name="operationTraces"> The operation traces used for touched digest. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The lowercase hexadecimal fingerprint string. </returns>
        public static string Compute (
            PlanTokenEnvironmentSnapshot snapshot,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var unityVersion = StringValueNormalizer.TrimOrFallback(snapshot.UnityVersion, NaLiteral);
            var compileState = StringValueNormalizer.TrimOrFallback(snapshot.CompileState, NaLiteral);
            var domainReloadGeneration = StringValueNormalizer.TrimOrFallback(snapshot.DomainReloadGeneration, NaLiteral);
            var configDigest = ComputeConfigDigest(snapshot.RepositoryRoot, cancellationToken);
            var touchedDigest = ComputeTouchedDigest(snapshot.ProjectRoot, operationTraces, cancellationToken);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteString("compileState", compileState);
                writer.WriteString("configDigest", configDigest);
                writer.WriteString("domainReloadGeneration", domainReloadGeneration);
                writer.WriteString("projectFingerprint", snapshot.ProjectFingerprint.ToString());
                writer.WriteString("touchedDigest", touchedDigest);
                writer.WriteString("unityVersion", unityVersion);
                writer.WriteEndObject();
                writer.Flush();
            }

            return Sha256LowerHex.Compute(stream.ToArray());
        }

        /// <summary> Computes configuration digest from shared <c>.ucli/config.json</c> fields. </summary>
        /// <param name="repositoryRoot"> The repository root path. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The lowercase hexadecimal digest string, or <c>na</c> when unavailable. </returns>
        private static string ComputeConfigDigest (
            string repositoryRoot,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var config = PlanTokenConfigResolver.Resolve(repositoryRoot);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("operationAllowlist");
                writer.WriteStartArray();
                for (var i = 0; i < config.OperationAllowlist.Count; i++)
                {
                    writer.WriteStringValue(config.OperationAllowlist[i]);
                }

                writer.WriteEndArray();
                writer.WriteString("operationPolicy", config.OperationPolicy);
                writer.WriteString("planTokenMode", config.PlanTokenModeLiteral);
                writer.WriteEndObject();
                writer.Flush();
            }

            return Sha256LowerHex.Compute(stream.ToArray());
        }

        /// <summary> Computes touched-resource digest from normalized touched entries and live file metadata. </summary>
        /// <param name="projectRoot"> The Unity project root path. </param>
        /// <param name="operationTraces"> The operation traces to inspect. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The lowercase hexadecimal digest string. </returns>
        private static string ComputeTouchedDigest (
            string projectRoot,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var touchedEntries = new List<PlanTokenTouchedDigestEntry>();
            for (var traceIndex = 0; traceIndex < operationTraces.Count; traceIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var trace = operationTraces[traceIndex];
                for (var touchedIndex = 0; touchedIndex < trace.Touched.Count; touchedIndex++)
                {
                    var touched = trace.Touched[touchedIndex];
                    touchedEntries.Add(CreateTouchedDigestEntry(projectRoot, touched));
                }
            }

            touchedEntries.Sort(static (x, y) =>
            {
                var kind = StringComparer.Ordinal.Compare(x.Kind, y.Kind);
                if (kind != 0)
                {
                    return kind;
                }

                var path = StringComparer.Ordinal.Compare(x.Path, y.Path);
                if (path != 0)
                {
                    return path;
                }

                return StringComparer.Ordinal.Compare(x.Guid, y.Guid);
            });

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartArray();
                for (var i = 0; i < touchedEntries.Count; i++)
                {
                    var entry = touchedEntries[i];
                    writer.WriteStartObject();
                    writer.WriteBoolean("exists", entry.Exists);
                    writer.WriteString("guid", entry.Guid);
                    writer.WriteString("kind", entry.Kind);
                    writer.WriteNumber("lastWriteUtcTicks", entry.LastWriteUtcTicks);
                    writer.WriteString("path", entry.Path);
                    writer.WriteNumber("size", entry.Size);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.Flush();
            }

            return Sha256LowerHex.Compute(stream.ToArray());
        }

        /// <summary> Creates one touched-digest entry from touched operation output. </summary>
        /// <param name="projectRoot"> The Unity project root path. </param>
        /// <param name="touched"> The touched operation output. </param>
        /// <returns> The digest entry. </returns>
        private static PlanTokenTouchedDigestEntry CreateTouchedDigestEntry (
            string projectRoot,
            OperationTouch touched)
        {
            var touchedPath = string.IsNullOrWhiteSpace(touched.Path) ? NaLiteral : touched.Path;
            var guid = StringValueNormalizer.TrimOrFallback(touched.Guid, NaLiteral);
            var normalizedPath = PathStringNormalizer.ToPlatformSeparated(touchedPath);
            var absolutePath = Path.Combine(projectRoot, normalizedPath);

            var exists = File.Exists(absolutePath) || Directory.Exists(absolutePath);
            long size;
            long lastWriteUtcTicks;
            if (File.Exists(absolutePath))
            {
                var fileInfo = new FileInfo(absolutePath);
                size = fileInfo.Length;
                lastWriteUtcTicks = fileInfo.LastWriteTimeUtc.Ticks;
            }
            else if (Directory.Exists(absolutePath))
            {
                var directoryInfo = new DirectoryInfo(absolutePath);
                size = -1;
                lastWriteUtcTicks = directoryInfo.LastWriteTimeUtc.Ticks;
            }
            else
            {
                size = -1;
                lastWriteUtcTicks = 0;
            }

            return new PlanTokenTouchedDigestEntry(
                Kind: touched.Kind.ToString().ToLowerInvariant(),
                Path: touchedPath,
                Guid: guid,
                Exists: exists,
                Size: size,
                LastWriteUtcTicks: lastWriteUtcTicks);
        }
    }
}
