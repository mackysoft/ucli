using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Coordinates plan-token issuance and validation around phase execution. </summary>
    internal interface IPlanTokenCoordinator
    {
        /// <summary> Issues one plan token from normalized request and plan traces. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The plan-phase operation traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The token issue result. </returns>
        PlanTokenIssueResult Issue (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default);

        /// <summary> Validates one incoming call plan token against request and current state. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The pre-call plan traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The validation result. </returns>
        PlanTokenValidationResult ValidateCall (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default);
    }

    /// <summary> Represents one plan-token issuance result. </summary>
    /// <param name="PlanToken"> The issued token string when issuance succeeded. </param>
    /// <param name="Failure"> The failure details when issuance failed. </param>
    internal sealed record PlanTokenIssueResult (
        string? PlanToken,
        OperationFailure? Failure)
    {
        /// <summary> Gets a value indicating whether issuance succeeded. </summary>
        public bool IsSuccess => Failure is null && !string.IsNullOrWhiteSpace(PlanToken);

        /// <summary> Creates a successful issuance result. </summary>
        /// <param name="planToken"> The issued token. </param>
        /// <returns> The successful result. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="planToken" /> is empty or whitespace. </exception>
        public static PlanTokenIssueResult Success (string planToken)
        {
            if (string.IsNullOrWhiteSpace(planToken))
            {
                throw new ArgumentException("Plan token must not be empty.", nameof(planToken));
            }

            return new PlanTokenIssueResult(planToken, null);
        }

        /// <summary> Creates a failed issuance result. </summary>
        /// <param name="failure"> The issuance failure details. </param>
        /// <returns> The failed result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
        public static PlanTokenIssueResult Failed (OperationFailure failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return new PlanTokenIssueResult(null, failure);
        }
    }

    /// <summary> Represents one plan-token validation result. </summary>
    /// <param name="Failure"> The validation failure details when validation failed; otherwise <see langword="null" />. </param>
    internal sealed record PlanTokenValidationResult (OperationFailure? Failure)
    {
        /// <summary> Gets a value indicating whether validation succeeded. </summary>
        public bool IsSuccess => Failure is null;

        /// <summary> Creates a successful validation result. </summary>
        /// <returns> The successful result. </returns>
        public static PlanTokenValidationResult Success ()
        {
            return new PlanTokenValidationResult((OperationFailure?)null);
        }

        /// <summary> Creates a failed validation result. </summary>
        /// <param name="failure"> The validation failure details. </param>
        /// <returns> The failed result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
        public static PlanTokenValidationResult Failed (OperationFailure failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return new PlanTokenValidationResult(failure);
        }
    }

    /// <summary> Captures runtime values required for plan-token generation and validation. </summary>
    internal interface IPlanTokenEnvironment
    {
        /// <summary> Captures one runtime environment snapshot. </summary>
        /// <returns> The captured snapshot. </returns>
        PlanTokenEnvironmentSnapshot Capture ();

        /// <summary> Gets the current UTC time. </summary>
        DateTimeOffset UtcNow { get; }
    }

    /// <summary> Represents one captured runtime snapshot used by plan-token workflows. </summary>
    /// <param name="ProjectRoot"> The Unity project root path. </param>
    /// <param name="RepositoryRoot"> The repository root path. </param>
    /// <param name="ProjectFingerprint"> The deterministic project fingerprint. </param>
    /// <param name="UnityVersion"> The current Unity version. </param>
    /// <param name="CompileState"> The current compile state. </param>
    /// <param name="DomainReloadGeneration"> The current domain-reload generation marker. </param>
    internal sealed record PlanTokenEnvironmentSnapshot (
        string ProjectRoot,
        string RepositoryRoot,
        string ProjectFingerprint,
        string UnityVersion,
        string CompileState,
        string DomainReloadGeneration);

    /// <summary> Default runtime environment provider used by plan-token workflows. </summary>
    internal sealed class DefaultPlanTokenEnvironment : IPlanTokenEnvironment
    {
        /// <summary> Gets the current UTC clock value. </summary>
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        /// <summary> Captures one runtime environment snapshot from Unity editor state. </summary>
        /// <returns> The captured snapshot. </returns>
        public PlanTokenEnvironmentSnapshot Capture ()
        {
            var projectRoot = ResolveProjectRoot();
            var repositoryRoot = ResolveRepositoryRoot(projectRoot);
            var projectFingerprint = UnityProjectFingerprintCalculatorCompat.Create(repositoryRoot, projectRoot);

            var unityVersion = string.IsNullOrWhiteSpace(Application.unityVersion)
                ? "na"
                : Application.unityVersion;
            var compileState = EditorApplication.isCompiling ? "compiling" : "ready";

            return new PlanTokenEnvironmentSnapshot(
                ProjectRoot: projectRoot,
                RepositoryRoot: repositoryRoot,
                ProjectFingerprint: projectFingerprint,
                UnityVersion: unityVersion,
                CompileState: compileState,
                DomainReloadGeneration: "na");
        }

        /// <summary> Resolves the current Unity project root path. </summary>
        /// <returns> The project root path. </returns>
        private static string ResolveProjectRoot ()
        {
            var dataPath = Application.dataPath;
            if (string.IsNullOrWhiteSpace(dataPath))
            {
                return Directory.GetCurrentDirectory();
            }

            var projectRoot = Path.GetDirectoryName(Path.GetFullPath(dataPath));
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return Directory.GetCurrentDirectory();
            }

            return projectRoot;
        }

        /// <summary> Resolves repository root by scanning parent directories for <c>.git</c>. </summary>
        /// <param name="projectRoot"> The Unity project root path. </param>
        /// <returns> The resolved repository root, or <paramref name="projectRoot" /> when no git marker is found. </returns>
        private static string ResolveRepositoryRoot (string projectRoot)
        {
            var currentDirectory = projectRoot;
            while (!string.IsNullOrWhiteSpace(currentDirectory))
            {
                var gitDirectoryPath = Path.Combine(currentDirectory, ".git");
                if (Directory.Exists(gitDirectoryPath) || File.Exists(gitDirectoryPath))
                {
                    return currentDirectory;
                }

                var parentDirectory = Directory.GetParent(currentDirectory);
                if (parentDirectory == null)
                {
                    break;
                }

                currentDirectory = parentDirectory.FullName;
            }

            return projectRoot;
        }
    }

    /// <summary> Calculates deterministic project fingerprints with the same algorithm as CLI runtime. </summary>
    internal static class UnityProjectFingerprintCalculatorCompat
    {
        /// <summary> Creates one deterministic SHA-256 fingerprint for storage and project paths. </summary>
        /// <param name="storageRoot"> The storage root path. </param>
        /// <param name="unityProjectRoot"> The Unity project root path. </param>
        /// <returns> The lowercase hexadecimal SHA-256 string. </returns>
        public static string Create (
            string storageRoot,
            string unityProjectRoot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
            }

            if (string.IsNullOrWhiteSpace(unityProjectRoot))
            {
                throw new ArgumentException("Unity project root must not be empty.", nameof(unityProjectRoot));
            }

            var normalizedStorageRoot = NormalizePath(storageRoot);
            var normalizedUnityProjectRoot = NormalizePath(unityProjectRoot);
            var projectPathFragment = BuildProjectPathFragment(normalizedStorageRoot, normalizedUnityProjectRoot);
            var fingerprintInput = $"{normalizedStorageRoot}\n{projectPathFragment}";
            return PlanTokenCoordinator.ComputeSha256Hex(Encoding.UTF8.GetBytes(fingerprintInput));
        }

        /// <summary> Builds one stable project-path fragment used in fingerprint input. </summary>
        /// <param name="normalizedStorageRoot"> The normalized storage root. </param>
        /// <param name="normalizedUnityProjectRoot"> The normalized Unity project root. </param>
        /// <returns> The project path fragment. </returns>
        private static string BuildProjectPathFragment (
            string normalizedStorageRoot,
            string normalizedUnityProjectRoot)
        {
            if (string.Equals(normalizedStorageRoot, normalizedUnityProjectRoot, PathComparison))
            {
                return ".";
            }

            if (IsUnderDirectory(normalizedUnityProjectRoot, normalizedStorageRoot))
            {
                var relativePath = Path.GetRelativePath(normalizedStorageRoot, normalizedUnityProjectRoot);
                return NormalizeRelativePath(relativePath);
            }

            return normalizedUnityProjectRoot;
        }

        /// <summary> Normalizes one path used by fingerprint input. </summary>
        /// <param name="pathValue"> The path value. </param>
        /// <returns> The normalized path. </returns>
        private static string NormalizePath (string pathValue)
        {
            var fullPath = Path.GetFullPath(pathValue);
            fullPath = fullPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var pathRoot = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(pathRoot) && string.Equals(fullPath, pathRoot, PathComparison))
            {
                return NormalizeCase(fullPath);
            }

            var trimmedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return NormalizeCase(trimmedPath);
        }

        /// <summary> Normalizes one relative path used by fingerprint input. </summary>
        /// <param name="relativePath"> The relative path value. </param>
        /// <returns> The normalized relative path. </returns>
        private static string NormalizeRelativePath (string relativePath)
        {
            var normalizedPath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (string.Equals(normalizedPath, ".", StringComparison.Ordinal))
            {
                return normalizedPath;
            }

            return normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary> Determines whether one path is under the specified directory path. </summary>
        /// <param name="path"> The candidate path. </param>
        /// <param name="directoryPath"> The directory path. </param>
        /// <returns> <see langword="true" /> when the path is under the directory. </returns>
        private static bool IsUnderDirectory (
            string path,
            string directoryPath)
        {
            if (!path.StartsWith(directoryPath, PathComparison))
            {
                return false;
            }

            var trailingDirectoryPath = directoryPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || directoryPath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? directoryPath
                : directoryPath + Path.DirectorySeparatorChar;
            return path.StartsWith(trailingDirectoryPath, PathComparison);
        }

        /// <summary> Normalizes path casing for platforms with case-insensitive path comparison. </summary>
        /// <param name="path"> The input path value. </param>
        /// <returns> The normalized path value. </returns>
        private static string NormalizeCase (string path)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? path.ToUpperInvariant()
                : path;
        }

        /// <summary> Gets path comparison mode for the current operating system. </summary>
        private static StringComparison PathComparison =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    /// <summary> Provides file-backed plan-token issuance and validation services. </summary>
    internal sealed class PlanTokenCoordinator : IPlanTokenCoordinator
    {
        private const string PlanTokenModeOptional = "optional";

        private const string PlanTokenModeRequired = "required";

        private const string TokenType = "ucli-plan-token";

        private const string TokenAlgorithm = "HS256";

        private const string TokenKeyId = "v1";

        private const int TokenVersion = 1;

        private const string UcliDirectoryName = ".ucli";

        private const string LocalDirectoryName = "local";

        private const string FingerprintsDirectoryName = "fingerprints";

        private const string PlanTokenKeyFileName = "plan-token.key";

        private const string ConfigFileName = "config.json";

        private static readonly TimeSpan DefaultTokenTtl = TimeSpan.FromMinutes(15);

        private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

        private readonly IPlanTokenEnvironment environment;

        /// <summary> Initializes a new instance of the <see cref="PlanTokenCoordinator" /> class. </summary>
        /// <param name="environment"> The runtime environment dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="environment" /> is <see langword="null" />. </exception>
        public PlanTokenCoordinator (IPlanTokenEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary> Initializes a new instance of the <see cref="PlanTokenCoordinator" /> class with default runtime environment. </summary>
        public PlanTokenCoordinator () : this(new DefaultPlanTokenEnvironment())
        {
        }

        /// <summary> Issues one plan token from normalized request and plan traces. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The plan-phase operation traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The token issue result. </returns>
        public PlanTokenIssueResult Issue (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var snapshot = environment.Capture();
                var requestDigest = ComputeRequestDigest(request);
                var stateFingerprint = ComputeStateFingerprint(snapshot, operationTraces, cancellationToken);

                if (!TryLoadOrCreateKey(snapshot, out var signingKey, out var keyErrorMessage))
                {
                    return PlanTokenIssueResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.InternalError,
                        Message: keyErrorMessage ?? "Failed to load plan-token signing key.",
                        OpId: null));
                }

                var issuedAtUtc = environment.UtcNow;
                var expiresAtUtc = issuedAtUtc.Add(DefaultTokenTtl);
                var payload = new PlanTokenPayload(
                    Version: TokenVersion,
                    KeyId: TokenKeyId,
                    ProjectFingerprint: snapshot.ProjectFingerprint,
                    RequestDigest: requestDigest,
                    StateFingerprint: stateFingerprint,
                    IssuedAtUtc: issuedAtUtc,
                    ExpiresAtUtc: expiresAtUtc,
                    Nonce: CreateNonce());

                var token = CreateSignedToken(signingKey, payload);
                return PlanTokenIssueResult.Success(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return PlanTokenIssueResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Failed to issue plan token. {exception.Message}",
                    OpId: null));
            }
        }

        /// <summary> Validates one incoming call plan token against request and current state. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The pre-call plan traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The validation result. </returns>
        public PlanTokenValidationResult ValidateCall (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var snapshot = environment.Capture();
                var configuredMode = ResolvePlanTokenMode(snapshot.RepositoryRoot);
                if (string.IsNullOrWhiteSpace(request.PlanToken))
                {
                    if (configuredMode == PlanTokenMode.Required)
                    {
                        return PlanTokenValidationResult.Failed(new OperationFailure(
                            Code: IpcErrorCodes.PlanTokenRequired,
                            Message: "Plan token is required for call execution.",
                            OpId: null));
                    }

                    return PlanTokenValidationResult.Success();
                }

                if (!TryParseTokenParts(request.PlanToken, out var headerSegment, out var payloadSegment, out var signatureSegment))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token format is invalid."));
                }

                if (!TryDecodeBase64Url(headerSegment, out var headerBytes)
                    || !TryDecodeBase64Url(payloadSegment, out var payloadBytes)
                    || !TryDecodeBase64Url(signatureSegment, out var signatureBytes))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token contains invalid base64url segments."));
                }

                if (!TryReadHeader(headerBytes, out var header))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token header is invalid."));
                }

                if (!TryReadPayload(payloadBytes, out var payload))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token payload is invalid."));
                }

                if (!string.Equals(header.Algorithm, TokenAlgorithm, StringComparison.Ordinal)
                    || !string.Equals(header.Type, TokenType, StringComparison.Ordinal)
                    || !string.Equals(header.KeyId, TokenKeyId, StringComparison.Ordinal)
                    || !string.Equals(payload.KeyId, TokenKeyId, StringComparison.Ordinal)
                    || payload.Version != TokenVersion)
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token header values are not supported."));
                }

                if (!TryLoadOrCreateKey(snapshot, out var signingKey, out var keyErrorMessage))
                {
                    return PlanTokenValidationResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.InternalError,
                        Message: keyErrorMessage ?? "Failed to load plan-token signing key.",
                        OpId: null));
                }

                var signingInput = headerSegment + "." + payloadSegment;
                var expectedSignature = ComputeSignature(signingInput, signingKey);
                if (!CryptographicOperations.FixedTimeEquals(expectedSignature, signatureBytes))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token signature is invalid."));
                }

                if (!string.Equals(payload.ProjectFingerprint, snapshot.ProjectFingerprint, StringComparison.Ordinal))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token project fingerprint does not match current project."));
                }

                var nowUtc = environment.UtcNow;
                if (nowUtc > payload.ExpiresAtUtc.Add(ClockSkew))
                {
                    return PlanTokenValidationResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.PlanTokenExpired,
                        Message: "Plan token has expired.",
                        OpId: null));
                }

                if (nowUtc < payload.IssuedAtUtc.Subtract(ClockSkew))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token issued-at timestamp is in the future."));
                }

                var requestDigest = ComputeRequestDigest(request);
                if (!string.Equals(requestDigest, payload.RequestDigest, StringComparison.Ordinal))
                {
                    return PlanTokenValidationResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.PlanTokenRequestMismatch,
                        Message: "Plan token request digest does not match current request.",
                        OpId: null));
                }

                var stateFingerprint = ComputeStateFingerprint(snapshot, operationTraces, cancellationToken);
                if (!string.Equals(stateFingerprint, payload.StateFingerprint, StringComparison.Ordinal))
                {
                    return PlanTokenValidationResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.StateChangedSincePlan,
                        Message: "Project state changed since plan token issuance.",
                        OpId: null));
                }

                return PlanTokenValidationResult.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return PlanTokenValidationResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Failed to validate plan token. {exception.Message}",
                    OpId: null));
            }
        }

        /// <summary> Computes deterministic request digest from normalized request canonical payload. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <returns> The lowercase hexadecimal digest string. </returns>
        private static string ComputeRequestDigest (NormalizedExecuteRequest request)
        {
            return ComputeSha256Hex(request.CanonicalDigestPayloadUtf8.Span);
        }

        /// <summary> Computes deterministic state fingerprint for token payload validation. </summary>
        /// <param name="snapshot"> The runtime environment snapshot. </param>
        /// <param name="operationTraces"> The operation traces used for touched digest. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The lowercase hexadecimal fingerprint string. </returns>
        private static string ComputeStateFingerprint (
            PlanTokenEnvironmentSnapshot snapshot,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var unityVersion = NormalizeOrFallback(snapshot.UnityVersion);
            var compileState = NormalizeOrFallback(snapshot.CompileState);
            var domainReloadGeneration = NormalizeOrFallback(snapshot.DomainReloadGeneration);
            var configDigest = ComputeConfigDigest(snapshot.RepositoryRoot, cancellationToken);
            var touchedDigest = ComputeTouchedDigest(snapshot.ProjectRoot, operationTraces, cancellationToken);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteString("compileState", compileState);
                writer.WriteString("configDigest", configDigest);
                writer.WriteString("domainReloadGeneration", domainReloadGeneration);
                writer.WriteString("projectFingerprint", NormalizeOrFallback(snapshot.ProjectFingerprint));
                writer.WriteString("touchedDigest", touchedDigest);
                writer.WriteString("unityVersion", unityVersion);
                writer.WriteEndObject();
                writer.Flush();
            }

            return ComputeSha256Hex(stream.ToArray());
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

            try
            {
                var configFilePath = Path.Combine(repositoryRoot, UcliDirectoryName, ConfigFileName);
                if (!File.Exists(configFilePath))
                {
                    return "na";
                }

                using var document = JsonDocument.Parse(File.ReadAllText(configFilePath));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return "na";
                }

                var operationPolicy = TryReadString(root, "operationPolicy") ?? "na";
                var planTokenMode = TryReadString(root, "planTokenMode") ?? "na";
                var allowlist = TryReadAllowlist(root);

                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("operationAllowlist");
                    writer.WriteStartArray();
                    for (var i = 0; i < allowlist.Count; i++)
                    {
                        writer.WriteStringValue(allowlist[i]);
                    }

                    writer.WriteEndArray();
                    writer.WriteString("operationPolicy", operationPolicy);
                    writer.WriteString("planTokenMode", planTokenMode);
                    writer.WriteEndObject();
                    writer.Flush();
                }

                return ComputeSha256Hex(stream.ToArray());
            }
            catch
            {
                return "na";
            }
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

            var touchedEntries = new List<TouchedDigestEntry>();
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

            return ComputeSha256Hex(stream.ToArray());
        }

        /// <summary> Creates one touched-digest entry from touched operation output. </summary>
        /// <param name="projectRoot"> The Unity project root path. </param>
        /// <param name="touched"> The touched operation output. </param>
        /// <returns> The digest entry. </returns>
        private static TouchedDigestEntry CreateTouchedDigestEntry (
            string projectRoot,
            OperationTouch touched)
        {
            var touchedPath = string.IsNullOrWhiteSpace(touched.Path) ? "na" : touched.Path;
            var guid = string.IsNullOrWhiteSpace(touched.Guid) ? "na" : touched.Guid;
            var normalizedPath = touchedPath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
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

            return new TouchedDigestEntry(
                Kind: touched.Kind.ToString().ToLowerInvariant(),
                Path: touchedPath,
                Guid: guid,
                Exists: exists,
                Size: size,
                LastWriteUtcTicks: lastWriteUtcTicks);
        }

        /// <summary> Creates a signed compact token string from payload values. </summary>
        /// <param name="signingKey"> The HMAC signing key. </param>
        /// <param name="payload"> The token payload values. </param>
        /// <returns> The compact token string. </returns>
        private static string CreateSignedToken (
            byte[] signingKey,
            PlanTokenPayload payload)
        {
            var headerBytes = CreateHeaderJsonBytes();
            var payloadBytes = CreatePayloadJsonBytes(payload);
            var headerSegment = EncodeBase64Url(headerBytes);
            var payloadSegment = EncodeBase64Url(payloadBytes);
            var signingInput = headerSegment + "." + payloadSegment;
            var signature = ComputeSignature(signingInput, signingKey);
            var signatureSegment = EncodeBase64Url(signature);
            return signingInput + "." + signatureSegment;
        }

        /// <summary> Creates compact-token header JSON bytes. </summary>
        /// <returns> The header JSON bytes. </returns>
        private static byte[] CreateHeaderJsonBytes ()
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteString("alg", TokenAlgorithm);
                writer.WriteString("kid", TokenKeyId);
                writer.WriteString("typ", TokenType);
                writer.WriteEndObject();
                writer.Flush();
            }

            return stream.ToArray();
        }

        /// <summary> Creates compact-token payload JSON bytes. </summary>
        /// <param name="payload"> The payload values. </param>
        /// <returns> The payload JSON bytes. </returns>
        private static byte[] CreatePayloadJsonBytes (PlanTokenPayload payload)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("v", payload.Version);
                writer.WriteString("kid", payload.KeyId);
                writer.WriteString("projectFingerprint", payload.ProjectFingerprint);
                writer.WriteString("requestDigest", payload.RequestDigest);
                writer.WriteString("stateFingerprint", payload.StateFingerprint);
                writer.WriteString("issuedAtUtc", payload.IssuedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("expiresAtUtc", payload.ExpiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("nonce", payload.Nonce);
                writer.WriteEndObject();
                writer.Flush();
            }

            return stream.ToArray();
        }

        /// <summary> Computes HMAC signature bytes for one compact-token signing input. </summary>
        /// <param name="signingInput"> The compact signing input text. </param>
        /// <param name="signingKey"> The signing key bytes. </param>
        /// <returns> The HMAC-SHA256 signature bytes. </returns>
        private static byte[] ComputeSignature (
            string signingInput,
            byte[] signingKey)
        {
            var signingInputBytes = Encoding.UTF8.GetBytes(signingInput);
            using var hmac = new HMACSHA256(signingKey);
            return hmac.ComputeHash(signingInputBytes);
        }

        /// <summary> Resolves configured plan-token mode from shared config. </summary>
        /// <param name="repositoryRoot"> The repository root path. </param>
        /// <returns> The resolved plan-token mode. </returns>
        private static PlanTokenMode ResolvePlanTokenMode (string repositoryRoot)
        {
            try
            {
                var configPath = Path.Combine(repositoryRoot, UcliDirectoryName, ConfigFileName);
                if (!File.Exists(configPath))
                {
                    return PlanTokenMode.Optional;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(configPath));
                var root = document.RootElement;
                var modeValue = TryReadString(root, "planTokenMode");
                if (string.Equals(modeValue, PlanTokenModeRequired, StringComparison.OrdinalIgnoreCase))
                {
                    return PlanTokenMode.Required;
                }

                if (string.Equals(modeValue, PlanTokenModeOptional, StringComparison.OrdinalIgnoreCase))
                {
                    return PlanTokenMode.Optional;
                }
            }
            catch
            {
                // NOTE:
                // Invalid or unreadable config falls back to optional mode by design.
            }

            return PlanTokenMode.Optional;
        }

        /// <summary> Loads one existing signing key or creates a new key file on demand. </summary>
        /// <param name="snapshot"> The runtime environment snapshot. </param>
        /// <param name="key"> The loaded or generated key bytes. </param>
        /// <param name="errorMessage"> The error message when load/create fails. </param>
        /// <returns> <see langword="true" /> when key is available; otherwise <see langword="false" />. </returns>
        private static bool TryLoadOrCreateKey (
            PlanTokenEnvironmentSnapshot snapshot,
            out byte[] key,
            out string? errorMessage)
        {
            try
            {
                var keyFilePath = BuildKeyFilePath(snapshot.RepositoryRoot, snapshot.ProjectFingerprint);
                var parentDirectory = Path.GetDirectoryName(keyFilePath);
                if (string.IsNullOrWhiteSpace(parentDirectory))
                {
                    key = Array.Empty<byte>();
                    errorMessage = "Failed to resolve plan-token key directory.";
                    return false;
                }

                Directory.CreateDirectory(parentDirectory);

                if (File.Exists(keyFilePath))
                {
                    var encodedKey = File.ReadAllText(keyFilePath).Trim();
                    if (TryDecodeKey(encodedKey, out key))
                    {
                        errorMessage = null;
                        return true;
                    }
                }

                key = CreateRandomKey();
                var encoded = Convert.ToBase64String(key);
                File.WriteAllText(keyFilePath, encoded);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                key = Array.Empty<byte>();
                errorMessage = $"Failed to initialize plan-token key. {exception.Message}";
                return false;
            }
        }

        /// <summary> Builds plan-token key file path from repository and fingerprint identity. </summary>
        /// <param name="repositoryRoot"> The repository root path. </param>
        /// <param name="projectFingerprint"> The project fingerprint value. </param>
        /// <returns> The key file path. </returns>
        private static string BuildKeyFilePath (
            string repositoryRoot,
            string projectFingerprint)
        {
            return Path.Combine(
                repositoryRoot,
                UcliDirectoryName,
                LocalDirectoryName,
                FingerprintsDirectoryName,
                projectFingerprint,
                PlanTokenKeyFileName);
        }

        /// <summary> Attempts to decode one stored key string. </summary>
        /// <param name="encoded"> The encoded key string. </param>
        /// <param name="key"> The decoded key bytes. </param>
        /// <returns> <see langword="true" /> when decode succeeds and size is valid; otherwise <see langword="false" />. </returns>
        private static bool TryDecodeKey (
            string encoded,
            out byte[] key)
        {
            key = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return false;
            }

            try
            {
                var decoded = Convert.FromBase64String(encoded);
                if (decoded.Length < 32)
                {
                    return false;
                }

                key = decoded;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Creates one new random signing key. </summary>
        /// <returns> The generated key bytes. </returns>
        private static byte[] CreateRandomKey ()
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            return key;
        }

        /// <summary> Creates one random nonce string for token payload uniqueness. </summary>
        /// <returns> The generated nonce string. </returns>
        private static string CreateNonce ()
        {
            var nonceBytes = new byte[16];
            RandomNumberGenerator.Fill(nonceBytes);
            return EncodeBase64Url(nonceBytes);
        }

        /// <summary> Parses compact-token segment strings. </summary>
        /// <param name="token"> The compact token string. </param>
        /// <param name="header"> The header segment. </param>
        /// <param name="payload"> The payload segment. </param>
        /// <param name="signature"> The signature segment. </param>
        /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryParseTokenParts (
            string token,
            out string header,
            out string payload,
            out string signature)
        {
            header = string.Empty;
            payload = string.Empty;
            signature = string.Empty;

            var segments = token.Split('.');
            if (segments.Length != 3)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(segments[0])
                || string.IsNullOrWhiteSpace(segments[1])
                || string.IsNullOrWhiteSpace(segments[2]))
            {
                return false;
            }

            header = segments[0];
            payload = segments[1];
            signature = segments[2];
            return true;
        }

        /// <summary> Attempts to read token header from JSON bytes. </summary>
        /// <param name="headerBytes"> The header JSON bytes. </param>
        /// <param name="header"> The parsed header model. </param>
        /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryReadHeader (
            byte[] headerBytes,
            out PlanTokenHeader header)
        {
            header = default;
            try
            {
                using var document = JsonDocument.Parse(headerBytes);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                var alg = TryReadString(root, "alg");
                var kid = TryReadString(root, "kid");
                var typ = TryReadString(root, "typ");
                if (string.IsNullOrWhiteSpace(alg)
                    || string.IsNullOrWhiteSpace(kid)
                    || string.IsNullOrWhiteSpace(typ))
                {
                    return false;
                }

                header = new PlanTokenHeader(alg, kid, typ);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Attempts to read token payload from JSON bytes. </summary>
        /// <param name="payloadBytes"> The payload JSON bytes. </param>
        /// <param name="payload"> The parsed payload model. </param>
        /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryReadPayload (
            byte[] payloadBytes,
            out PlanTokenPayload payload)
        {
            payload = default;
            try
            {
                using var document = JsonDocument.Parse(payloadBytes);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                if (!root.TryGetProperty("v", out var versionElement)
                    || !versionElement.TryGetInt32(out var version))
                {
                    return false;
                }

                var kid = TryReadString(root, "kid");
                var projectFingerprint = TryReadString(root, "projectFingerprint");
                var requestDigest = TryReadString(root, "requestDigest");
                var stateFingerprint = TryReadString(root, "stateFingerprint");
                var issuedAt = TryReadString(root, "issuedAtUtc");
                var expiresAt = TryReadString(root, "expiresAtUtc");
                var nonce = TryReadString(root, "nonce");

                if (string.IsNullOrWhiteSpace(kid)
                    || string.IsNullOrWhiteSpace(projectFingerprint)
                    || string.IsNullOrWhiteSpace(requestDigest)
                    || string.IsNullOrWhiteSpace(stateFingerprint)
                    || string.IsNullOrWhiteSpace(issuedAt)
                    || string.IsNullOrWhiteSpace(expiresAt)
                    || string.IsNullOrWhiteSpace(nonce))
                {
                    return false;
                }

                if (!DateTimeOffset.TryParse(
                    issuedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var issuedAtUtc))
                {
                    return false;
                }

                if (!DateTimeOffset.TryParse(
                    expiresAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var expiresAtUtc))
                {
                    return false;
                }

                payload = new PlanTokenPayload(
                    Version: version,
                    KeyId: kid,
                    ProjectFingerprint: projectFingerprint,
                    RequestDigest: requestDigest,
                    StateFingerprint: stateFingerprint,
                    IssuedAtUtc: issuedAtUtc,
                    ExpiresAtUtc: expiresAtUtc,
                    Nonce: nonce);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Attempts to read one optional string property from JSON object. </summary>
        /// <param name="jsonObject"> The JSON object. </param>
        /// <param name="propertyName"> The property name. </param>
        /// <returns> The string value when present and valid; otherwise <see langword="null" />. </returns>
        private static string? TryReadString (
            JsonElement jsonObject,
            string propertyName)
        {
            if (!jsonObject.TryGetProperty(propertyName, out var valueElement)
                || valueElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = valueElement.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        /// <summary> Attempts to read operation allowlist values from config root. </summary>
        /// <param name="root"> The config root object. </param>
        /// <returns> The normalized allowlist values. </returns>
        private static List<string> TryReadAllowlist (JsonElement root)
        {
            var values = new List<string>();
            if (!root.TryGetProperty("operationAllowlist", out var allowlistElement)
                || allowlistElement.ValueKind != JsonValueKind.Array)
            {
                values.Add("na");
                return values;
            }

            foreach (var allowlistValue in allowlistElement.EnumerateArray())
            {
                if (allowlistValue.ValueKind != JsonValueKind.String)
                {
                    values.Add("na");
                    return values;
                }

                var pattern = allowlistValue.GetString();
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                values.Add(pattern.Trim());
            }

            return values;
        }

        /// <summary> Encodes bytes as unpadded base64url text. </summary>
        /// <param name="bytes"> The input bytes. </param>
        /// <returns> The base64url text. </returns>
        private static string EncodeBase64Url (byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary> Decodes one base64url text into bytes. </summary>
        /// <param name="text"> The base64url text. </param>
        /// <param name="bytes"> The decoded bytes. </param>
        /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryDecodeBase64Url (
            string text,
            out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var base64 = text
                .Replace('-', '+')
                .Replace('_', '/');
            var padding = base64.Length % 4;
            if (padding == 2)
            {
                base64 += "==";
            }
            else if (padding == 3)
            {
                base64 += "=";
            }
            else if (padding != 0)
            {
                return false;
            }

            try
            {
                bytes = Convert.FromBase64String(base64);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Normalizes one string value or returns fallback literal when missing. </summary>
        /// <param name="value"> The input value. </param>
        /// <returns> The normalized value. </returns>
        private static string NormalizeOrFallback (string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "na" : value.Trim();
        }

        /// <summary> Creates one invalid-token failure entry. </summary>
        /// <param name="message"> The failure message. </param>
        /// <returns> The failure entry. </returns>
        private static OperationFailure CreateInvalidTokenFailure (string message)
        {
            return new OperationFailure(
                Code: IpcErrorCodes.PlanTokenInvalid,
                Message: message,
                OpId: null);
        }

        /// <summary> Computes SHA-256 digest and returns lowercase hexadecimal text. </summary>
        /// <param name="bytes"> The input bytes. </param>
        /// <returns> The lowercase hexadecimal digest string. </returns>
        public static string ComputeSha256Hex (ReadOnlySpan<byte> bytes)
        {
            var inputBytes = bytes.ToArray();
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(inputBytes);

            var chars = new char[hashBytes.Length * 2];
            var charIndex = 0;
            for (var i = 0; i < hashBytes.Length; i++)
            {
                var value = hashBytes[i];
                chars[charIndex] = ToHexNibble(value >> 4);
                chars[charIndex + 1] = ToHexNibble(value & 0x0f);
                charIndex += 2;
            }

            return new string(chars);
        }

        /// <summary> Converts one nibble value to lowercase hexadecimal char. </summary>
        /// <param name="value"> The nibble value. </param>
        /// <returns> The lowercase hexadecimal char. </returns>
        private static char ToHexNibble (int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }

        /// <summary> Represents one compact-token header model. </summary>
        /// <param name="Algorithm"> The signing algorithm identifier. </param>
        /// <param name="KeyId"> The key identifier. </param>
        /// <param name="Type"> The token type identifier. </param>
        private sealed record PlanTokenHeader (
            string Algorithm,
            string KeyId,
            string Type);

        /// <summary> Represents one compact-token payload model. </summary>
        /// <param name="Version"> The token format version. </param>
        /// <param name="KeyId"> The key identifier. </param>
        /// <param name="ProjectFingerprint"> The project fingerprint marker. </param>
        /// <param name="RequestDigest"> The request digest marker. </param>
        /// <param name="StateFingerprint"> The state fingerprint marker. </param>
        /// <param name="IssuedAtUtc"> The token issue time. </param>
        /// <param name="ExpiresAtUtc"> The token expiration time. </param>
        /// <param name="Nonce"> The nonce value. </param>
        private sealed record PlanTokenPayload (
            int Version,
            string KeyId,
            string ProjectFingerprint,
            string RequestDigest,
            string StateFingerprint,
            DateTimeOffset IssuedAtUtc,
            DateTimeOffset ExpiresAtUtc,
            string Nonce);

        /// <summary> Represents one touched-digest entry. </summary>
        /// <param name="Kind"> The touched kind literal. </param>
        /// <param name="Path"> The touched project-relative path. </param>
        /// <param name="Guid"> The touched guid value or <c>na</c>. </param>
        /// <param name="Exists"> Whether touched path exists at observation time. </param>
        /// <param name="Size"> The touched file size, or <c>-1</c> when unavailable. </param>
        /// <param name="LastWriteUtcTicks"> The last-write timestamp ticks in UTC, or <c>0</c> when unavailable. </param>
        private sealed record TouchedDigestEntry (
            string Kind,
            string Path,
            string Guid,
            bool Exists,
            long Size,
            long LastWriteUtcTicks);

        /// <summary> Defines resolved plan-token modes used by call validation. </summary>
        private enum PlanTokenMode
        {
            Optional = 0,
            Required = 1,
        }
    }

    /// <summary> Executes normalized operations through <c>validate -&gt; plan -&gt; call</c> phase pipelines. </summary>
    internal sealed class OperationPhaseExecutor : IOperationPhaseExecutor
    {
        private readonly IPhaseOperationRegistry operationRegistry;

        private readonly IPlanTokenCoordinator planTokenCoordinator;

        /// <summary> Initializes a new instance of the <see cref="OperationPhaseExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationRegistry" /> is <see langword="null" />. </exception>
        public OperationPhaseExecutor (IPhaseOperationRegistry operationRegistry)
            : this(operationRegistry, new PlanTokenCoordinator())
        {
        }

        /// <summary> Initializes a new instance of the <see cref="OperationPhaseExecutor" /> class. </summary>
        /// <param name="operationRegistry"> The phase-operation registry dependency. </param>
        /// <param name="planTokenCoordinator"> The plan-token coordination dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public OperationPhaseExecutor (
            IPhaseOperationRegistry operationRegistry,
            IPlanTokenCoordinator planTokenCoordinator)
        {
            this.operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
            this.planTokenCoordinator = planTokenCoordinator ?? throw new ArgumentNullException(nameof(planTokenCoordinator));
        }

        /// <summary> Executes one normalized request through the specified command phase-flow. </summary>
        /// <param name="command"> The top-level execution command. </param>
        /// <param name="request"> The normalized request. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The request-level execution trace. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        /// <exception cref="System.OperationCanceledException"> Thrown when execution is canceled. </exception>
        public async Task<PhaseExecutionTrace> Execute (
            PhaseExecutionCommand command,
            NormalizedExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var planPassResult = await ExecutePlanPass(request, cancellationToken).ConfigureAwait(false);
            if (!planPassResult.IsSuccess)
            {
                return PhaseExecutionTrace.Failure(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    operationTraces: planPassResult.OperationTraces,
                    errors: planPassResult.Errors);
            }

            if (command == PhaseExecutionCommand.Plan)
            {
                var issueResult = planTokenCoordinator.Issue(request, planPassResult.OperationTraces, cancellationToken);
                if (!issueResult.IsSuccess)
                {
                    return PhaseExecutionTrace.Failure(
                        protocolVersion: request.ProtocolVersion,
                        requestId: request.RequestId,
                        operationTraces: planPassResult.OperationTraces,
                        errors: new[]
                        {
                            issueResult.Failure!,
                        });
                }

                return PhaseExecutionTrace.Success(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    operationTraces: planPassResult.OperationTraces,
                    planToken: issueResult.PlanToken);
            }

            var validationResult = planTokenCoordinator.ValidateCall(request, planPassResult.OperationTraces, cancellationToken);
            if (!validationResult.IsSuccess)
            {
                return PhaseExecutionTrace.Failure(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    operationTraces: planPassResult.OperationTraces,
                    errors: new[]
                    {
                        validationResult.Failure!,
                    });
            }

            var callPassResult = await ExecuteCallPass(planPassResult.PreparedOperations, cancellationToken).ConfigureAwait(false);
            return callPassResult.IsSuccess
                ? PhaseExecutionTrace.Success(request.ProtocolVersion, request.RequestId, callPassResult.OperationTraces)
                : PhaseExecutionTrace.Failure(request.ProtocolVersion, request.RequestId, callPassResult.OperationTraces, callPassResult.Errors);
        }

        /// <summary> Executes validate and plan phases for all operations with fail-fast semantics. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The plan-pass result. </returns>
        private async Task<PlanPassResult> ExecutePlanPass (
            NormalizedExecuteRequest request,
            CancellationToken cancellationToken)
        {
            var operationTraces = new List<OperationPhaseTrace>(request.Ops.Count);
            var errors = new List<OperationFailure>(1);
            var preparedOperations = new List<PreparedOperation>(request.Ops.Count);
            var hasFailed = false;

            for (var i = 0; i < request.Ops.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var operation = request.Ops[i];
                if (hasFailed)
                {
                    operationTraces.Add(CreateSkippedTrace(operation));
                    continue;
                }

                if (!operationRegistry.TryResolve(operation.Op, out var phaseOperation))
                {
                    var missingOperationFailure = new OperationFailure(
                        Code: IpcErrorCodes.CommandNotImplemented,
                        Message: $"Operation '{operation.Op}' is not implemented.",
                        OpId: operation.Id);
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: false,
                        Changed: false,
                        Touched: Array.Empty<OperationTouch>(),
                        Failure: missingOperationFailure));
                    errors.Add(missingOperationFailure);
                    hasFailed = true;
                    continue;
                }

                var touched = new List<OperationTouch>();
                var validateStepResult = await ExecutePhaseStep(
                    operation,
                    OperationPhase.Validate,
                    ct => phaseOperation.Validate(operation, ct),
                    cancellationToken).ConfigureAwait(false);
                MergeTouched(touched, validateStepResult.Touched);
                if (!validateStepResult.IsSuccess)
                {
                    var touchedSnapshot = touched.ToArray();
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Validate,
                        Applied: validateStepResult.Applied,
                        Changed: validateStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: validateStepResult.Failure));
                    errors.Add(validateStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                var planStepResult = await ExecutePhaseStep(
                    operation,
                    OperationPhase.Plan,
                    ct => phaseOperation.Plan(operation, ct),
                    cancellationToken).ConfigureAwait(false);
                MergeTouched(touched, planStepResult.Touched);
                if (!planStepResult.IsSuccess)
                {
                    var touchedSnapshot = touched.ToArray();
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: operation.Id,
                        Op: operation.Op,
                        Phase: OperationPhase.Plan,
                        Applied: planStepResult.Applied,
                        Changed: planStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: planStepResult.Failure));
                    errors.Add(planStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                var successfulTouched = touched.ToArray();
                operationTraces.Add(new OperationPhaseTrace(
                    OpId: operation.Id,
                    Op: operation.Op,
                    Phase: OperationPhase.Plan,
                    Applied: planStepResult.Applied,
                    Changed: planStepResult.Changed,
                    Touched: successfulTouched,
                    Failure: null));
                preparedOperations.Add(new PreparedOperation(
                    Operation: operation,
                    PhaseOperation: phaseOperation,
                    PlanTouched: successfulTouched));
            }

            return new PlanPassResult(
                OperationTraces: operationTraces,
                Errors: errors,
                PreparedOperations: preparedOperations);
        }

        /// <summary> Executes call phase for prevalidated and preplanned operations. </summary>
        /// <param name="preparedOperations"> The prepared operations. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The call-pass result. </returns>
        private static async Task<CallPassResult> ExecuteCallPass (
            IReadOnlyList<PreparedOperation> preparedOperations,
            CancellationToken cancellationToken)
        {
            var operationTraces = new List<OperationPhaseTrace>(preparedOperations.Count);
            var errors = new List<OperationFailure>(1);
            var hasFailed = false;

            for (var i = 0; i < preparedOperations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var preparedOperation = preparedOperations[i];
                if (hasFailed)
                {
                    operationTraces.Add(CreateSkippedTrace(preparedOperation.Operation));
                    continue;
                }

                var callStepResult = await ExecutePhaseStep(
                    preparedOperation.Operation,
                    OperationPhase.Call,
                    ct => preparedOperation.PhaseOperation.Call(preparedOperation.Operation, ct),
                    cancellationToken).ConfigureAwait(false);

                var touched = new List<OperationTouch>(preparedOperation.PlanTouched.Count + callStepResult.Touched.Count);
                MergeTouched(touched, preparedOperation.PlanTouched);
                MergeTouched(touched, callStepResult.Touched);
                var touchedSnapshot = touched.ToArray();

                if (!callStepResult.IsSuccess)
                {
                    operationTraces.Add(new OperationPhaseTrace(
                        OpId: preparedOperation.Operation.Id,
                        Op: preparedOperation.Operation.Op,
                        Phase: OperationPhase.Call,
                        Applied: callStepResult.Applied,
                        Changed: callStepResult.Changed,
                        Touched: touchedSnapshot,
                        Failure: callStepResult.Failure));
                    errors.Add(callStepResult.Failure!);
                    hasFailed = true;
                    continue;
                }

                operationTraces.Add(new OperationPhaseTrace(
                    OpId: preparedOperation.Operation.Id,
                    Op: preparedOperation.Operation.Op,
                    Phase: OperationPhase.Call,
                    Applied: callStepResult.Applied,
                    Changed: callStepResult.Changed,
                    Touched: touchedSnapshot,
                    Failure: null));
            }

            return new CallPassResult(
                OperationTraces: operationTraces,
                Errors: errors);
        }

        /// <summary> Executes one phase step with exception-to-failure translation. </summary>
        /// <param name="operation"> The normalized operation. </param>
        /// <param name="phase"> The phase being executed. </param>
        /// <param name="executor"> The step executor delegate. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
        /// <returns> The phase-step result. </returns>
        private static async Task<OperationPhaseStepResult> ExecutePhaseStep (
            NormalizedOperation operation,
            OperationPhase phase,
            Func<CancellationToken, Task<OperationPhaseStepResult>> executor,
            CancellationToken cancellationToken)
        {
            try
            {
                var stepResult = await executor(cancellationToken).ConfigureAwait(false);
                if (stepResult == null)
                {
                    return OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.InternalError,
                        Message: $"Operation '{operation.Id}' returned null result at phase '{phase}'.",
                        OpId: operation.Id));
                }

                return stepResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return OperationPhaseStepResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Unexpected error occurred in operation '{operation.Id}' at phase '{phase}'. {exception.Message}",
                    OpId: operation.Id));
            }
        }

        /// <summary> Merges touched entries into one target list. </summary>
        /// <param name="target"> The target touched-entry list. </param>
        /// <param name="source"> The source touched-entry collection. </param>
        private static void MergeTouched (
            List<OperationTouch> target,
            IReadOnlyList<OperationTouch> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        /// <summary> Creates a skipped trace for operations after fail-fast stopping. </summary>
        /// <param name="operation"> The skipped operation. </param>
        /// <returns> The skipped trace entry. </returns>
        private static OperationPhaseTrace CreateSkippedTrace (NormalizedOperation operation)
        {
            return new OperationPhaseTrace(
                OpId: operation.Id,
                Op: operation.Op,
                Phase: OperationPhase.Skipped,
                Applied: false,
                Changed: false,
                Touched: Array.Empty<OperationTouch>(),
                Failure: null);
        }

        /// <summary> Represents one preplanned operation prepared by validate/plan pass. </summary>
        /// <param name="Operation"> The normalized operation model. </param>
        /// <param name="PhaseOperation"> The resolved phase operation implementation. </param>
        /// <param name="PlanTouched"> The touched list produced by validate and plan phases. </param>
        private sealed record PreparedOperation (
            NormalizedOperation Operation,
            IPhaseOperation PhaseOperation,
            IReadOnlyList<OperationTouch> PlanTouched);

        /// <summary> Represents one validate/plan pass result. </summary>
        /// <param name="OperationTraces"> The per-operation traces from validate/plan pass. </param>
        /// <param name="Errors"> The validate/plan pass errors. </param>
        /// <param name="PreparedOperations"> The operations prepared for call-phase execution. </param>
        private sealed record PlanPassResult (
            IReadOnlyList<OperationPhaseTrace> OperationTraces,
            IReadOnlyList<OperationFailure> Errors,
            IReadOnlyList<PreparedOperation> PreparedOperations)
        {
            /// <summary> Gets a value indicating whether validate/plan pass succeeded. </summary>
            public bool IsSuccess => Errors.Count == 0;
        }

        /// <summary> Represents one call-pass result. </summary>
        /// <param name="OperationTraces"> The per-operation traces from call pass. </param>
        /// <param name="Errors"> The call-pass errors. </param>
        private sealed record CallPassResult (
            IReadOnlyList<OperationPhaseTrace> OperationTraces,
            IReadOnlyList<OperationFailure> Errors)
        {
            /// <summary> Gets a value indicating whether call pass succeeded. </summary>
            public bool IsSuccess => Errors.Count == 0;
        }
    }
}
