using System.Text;
using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Application.Features.Recording.Artifacts;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Features.Recording.Requests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Features.Recording.Artifacts.Mp4;
using MackySoft.Ucli.Infrastructure.Artifacts;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Recording.Artifacts;

/// <summary> Owns provider work and immutable host artifacts for GameView recordings. </summary>
internal sealed class FileGameViewRecordingArtifactStore : IGameViewRecordingArtifactStore
{
    private readonly ImmutableArtifactFilePublisher artifactPublisher;
    private readonly GameViewRecordingMp4Validator mp4Validator;

    /// <summary> Initializes the recording artifact store. </summary>
    public FileGameViewRecordingArtifactStore (
        ImmutableArtifactFilePublisher artifactPublisher,
        GameViewRecordingMp4Validator mp4Validator)
    {
        this.artifactPublisher = artifactPublisher
            ?? throw new ArgumentNullException(nameof(artifactPublisher));
        this.mp4Validator = mp4Validator
            ?? throw new ArgumentNullException(nameof(mp4Validator));
    }

    /// <inheritdoc />
    public GameViewRecordingArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        Guid recordingId,
        IGameViewRecordingAdmissionLease admissionLease)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(admissionLease);
        if (recordingId == Guid.Empty)
        {
            return GameViewRecordingArtifactPreparationResult.Failure(
                ExecutionError.InvalidArgument(
                    "Recording identifier must not be empty.",
                    UcliCoreErrorCodes.InvalidArgument));
        }
        if (admissionLease.Project != unityProject
            || admissionLease.RecordingId != recordingId)
        {
            throw new ArgumentException(
                "Recording artifact preparation requires the admission lease for the same project and identifier.",
                nameof(admissionLease));
        }

        RecordingArtifactPaths? paths = null;
        try
        {
            paths = ResolvePaths(unityProject, recordingId);
            EnsureExpectedLayout(paths);
            if (PathExists(paths.ArtifactDirectory)
                || PathExists(paths.ExecutionWorkDirectory))
            {
                RecoverUnregisteredPreparation(paths);
            }
            else
            {
                FileSystemAccessBoundary.EnsureSecureDirectory(paths.ArtifactDirectory);
                FileSystemAccessBoundary.EnsureSecureDirectory(paths.DiagnosticsDirectory);
                FileSystemAccessBoundary.EnsureSecureDirectory(paths.ProviderWorkDirectory);
            }

            return GameViewRecordingArtifactPreparationResult.Success(
                new RecordingArtifactLease(this, paths));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            if (paths is not null)
            {
                TryDeleteKnownEmptyPreparationDirectories(paths);
            }

            return GameViewRecordingArtifactPreparationResult.Failure(
                ExecutionError.InternalError(
                    $"Failed to prepare GameView recording artifact storage. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public GameViewRecordingArtifactOpenResult Open (
        ResolvedUnityProjectContext unityProject,
        Guid recordingId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        if (recordingId == Guid.Empty)
        {
            return GameViewRecordingArtifactOpenResult.Failure(
                ExecutionError.InvalidArgument(
                    "Recording identifier must not be empty.",
                    UcliCoreErrorCodes.InvalidArgument));
        }

        try
        {
            var paths = ResolvePaths(unityProject, recordingId);
            EnsureExpectedLayout(paths);
            EnsureExistingSecureDirectory(paths.ArtifactDirectory, "Recording artifact directory");
            EnsureExistingSecureDirectory(paths.DiagnosticsDirectory, "Recording diagnostics directory");
            EnsureExistingSecureDirectory(paths.ExecutionWorkDirectory, "Recording work directory");
            if (Directory.Exists(paths.ProviderWorkDirectory.Value))
            {
                EnsureExistingSecureDirectory(paths.ProviderWorkDirectory, "Recording provider work directory");
            }
            else if (File.Exists(paths.ProviderWorkDirectory.Value))
            {
                throw new IOException("Recording provider work path is not a directory.");
            }

            return GameViewRecordingArtifactOpenResult.Success(
                new RecordingArtifactLease(this, paths));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            return GameViewRecordingArtifactOpenResult.Failure(
                ExecutionError.InternalError(
                    $"Failed to open GameView recording artifact storage. {exception.Message}"));
        }
    }

    private async ValueTask<GameViewRecordingArtifactPublicationResult> PublishRequestAsync (
        RecordingArtifactPaths paths,
        GameViewRecordingEffectiveRequest request,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var contract = new GameViewRecordingRequest(
                request.SchemaVersion,
                request.Resolution,
                request.FrameRate,
                request.MaxDurationSeconds);
            var canonicalBytes = CreateCanonicalJsonBytes(contract);
            var normalizedBytes = Encoding.UTF8.GetBytes(request.CanonicalJson);
            if (!canonicalBytes.AsSpan().SequenceEqual(normalizedBytes)
                || Sha256Digest.Compute(canonicalBytes) != request.Digest)
            {
                throw new InvalidDataException(
                    "Normalized recording request bytes do not match the effective typed request and digest.");
            }

            return await PublishJsonAsync(
                    paths,
                    contract,
                    canonicalBytes,
                    GameViewRecordingArtifactKinds.Request,
                    paths.RequestPath,
                    knownArtifact,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsArtifactFailure(exception))
        {
            return PublicationFailure("recording request", exception);
        }
    }

    private async ValueTask<GameViewRecordingVideoPublicationResult> PublishVideoAsync (
        RecordingArtifactPaths paths,
        GameViewRecordingEffectiveRequest request,
        int? observedEncodedFrameCount,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (observedEncodedFrameCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observedEncodedFrameCount),
                observedEncodedFrameCount,
                "Observed encoded frame count must not be negative.");
        }

        try
        {
            EnsureExpectedLayout(paths);
            EnsureExistingSecureDirectory(paths.ArtifactDirectory, "Recording artifact directory");
            if (File.Exists(paths.VideoPath.Value) || Directory.Exists(paths.VideoPath.Value))
            {
                if (knownArtifact is not null)
                {
                    GameViewRecordingMp4ValidationResult? existingValidation = null;
                    var existingArtifact = await VerifyExistingArtifactAsync(
                            paths,
                            paths.VideoPath,
                            knownArtifact,
                            GameViewRecordingArtifactKinds.Video,
                            GameViewRecordingArtifactMediaTypes.Mp4,
                            async (stream, token) =>
                            {
                                existingValidation = await mp4Validator.ValidateAsync(
                                        stream,
                                        request.Resolution.Width,
                                        request.Resolution.Height,
                                        request.FrameRate,
                                        request.MaxDurationSeconds,
                                        token)
                                    .ConfigureAwait(false);
                                EnsureObservedFrameCount(existingValidation, observedEncodedFrameCount);
                            },
                            cancellationToken)
                        .ConfigureAwait(false);
                    return GameViewRecordingVideoPublicationResult.Success(
                        CreateVideoPublication(existingArtifact, existingValidation!));
                }

                EnsureExistingSecureDirectory(paths.ProviderWorkDirectory, "Recording provider work directory");
                using var recoveryProviderOutput = OpenProviderOutput(paths, cancellationToken);
                var providerMeasurement = await recoveryProviderOutput
                    .MeasureAsync(cancellationToken)
                    .ConfigureAwait(false);
                await RemoveUncheckpointedArtifactAsync(
                        paths,
                        paths.VideoPath,
                        async (stream, token) =>
                        {
                            var validation = await mp4Validator.ValidateAsync(
                                    stream,
                                    request.Resolution.Width,
                                    request.Resolution.Height,
                                    request.FrameRate,
                                    request.MaxDurationSeconds,
                                    token)
                                .ConfigureAwait(false);
                            EnsureObservedFrameCount(validation, observedEncodedFrameCount);
                        },
                        providerMeasurement,
                        cancellationToken)
                    .ConfigureAwait(false);
                recoveryProviderOutput.EnsureStillBound();
            }
            if (knownArtifact is not null)
            {
                throw new IOException("Known recording video artifact is missing from its immutable path.");
            }

            EnsureExistingSecureDirectory(paths.ProviderWorkDirectory, "Recording provider work directory");
            using var providerOutput = OpenProviderOutput(paths, cancellationToken);

            GameViewRecordingMp4ValidationResult? providerValidation = null;
            await providerOutput.ValidateAsync(
                    async (stream, token) =>
                    {
                        providerValidation = await mp4Validator.ValidateAsync(
                                stream,
                                request.Resolution.Width,
                                request.Resolution.Height,
                                request.FrameRate,
                                request.MaxDurationSeconds,
                                token)
                            .ConfigureAwait(false);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            EnsureObservedFrameCount(providerValidation!, observedEncodedFrameCount);

            GameViewRecordingMp4ValidationResult? publishedValidation = null;
            var artifact = await artifactPublisher.PublishAsync(
                    GameViewRecordingArtifactKinds.Video,
                    GameViewRecordingArtifactMediaTypes.Mp4,
                    ContainedPath.Create(paths.RepositoryRoot, paths.VideoPath),
                    (destination, token) => CopyProviderOutputAsync(
                        providerOutput,
                        destination,
                        token),
                    async (stream, token) =>
                    {
                        publishedValidation = await mp4Validator.ValidateAsync(
                                stream,
                                request.Resolution.Width,
                                request.Resolution.Height,
                                request.FrameRate,
                                request.MaxDurationSeconds,
                                token)
                            .ConfigureAwait(false);
                        EnsureObservedFrameCount(publishedValidation, observedEncodedFrameCount);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            providerOutput.EnsureStillBound();

            if (publishedValidation is null || publishedValidation != providerValidation)
            {
                throw new InvalidDataException(
                    "Published MP4 observations do not match the held provider output.");
            }

            return GameViewRecordingVideoPublicationResult.Success(
                CreateVideoPublication(artifact, publishedValidation));
        }
        catch (Exception exception) when (IsArtifactFailure(exception))
        {
            return GameViewRecordingVideoPublicationResult.Failure(
                ExecutionError.InternalError(
                    $"Failed to validate and publish the GameView recording MP4. {exception.Message}",
                    GameViewRecordingErrorCodes.FinalizationFailed));
        }
    }

    private ValueTask<GameViewRecordingArtifactPublicationResult> PublishManifestAsync (
        RecordingArtifactPaths paths,
        GameViewRecordingManifest manifest,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return PublishTypedJsonWithErrorBoundaryAsync(
            paths,
            manifest,
            GameViewRecordingArtifactKinds.Manifest,
            paths.ManifestPath,
            "recording manifest",
            knownArtifact,
            cancellationToken);
    }

    private ValueTask<GameViewRecordingArtifactPublicationResult> PublishCleanupAsync (
        RecordingArtifactPaths paths,
        GameViewRecordingCleanupRecord cleanup,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        return PublishTypedJsonWithErrorBoundaryAsync(
            paths,
            cleanup,
            GameViewRecordingArtifactKinds.Cleanup,
            paths.CleanupPath,
            "recording cleanup record",
            knownArtifact,
            cancellationToken);
    }

    private ValueTask<GameViewRecordingArtifactPublicationResult> PublishTerminalRecordAsync (
        RecordingArtifactPaths paths,
        GameViewRecordingTerminalRecord terminalRecord,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(terminalRecord);
        return PublishTypedJsonWithErrorBoundaryAsync(
            paths,
            terminalRecord,
            GameViewRecordingArtifactKinds.TerminalRecord,
            paths.TerminalPath,
            "recording terminal record",
            knownArtifact,
            cancellationToken);
    }

    private async ValueTask<GameViewRecordingArtifactPublicationResult> PublishTypedJsonWithErrorBoundaryAsync<T> (
        RecordingArtifactPaths paths,
        T value,
        ArtifactKind kind,
        AbsolutePath destination,
        string subject,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken)
        where T : notnull
    {
        try
        {
            return await PublishJsonAsync(
                    paths,
                    value,
                    CreateCanonicalJsonBytes(value),
                    kind,
                    destination,
                    knownArtifact,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsArtifactFailure(exception))
        {
            return PublicationFailure(subject, exception);
        }
    }

    private async ValueTask<GameViewRecordingPartialOutputRecoveryResult> RecoverPartialOutputAsync (
        RecordingArtifactPaths paths,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken)
    {
        try
        {
            EnsureExpectedLayout(paths);
            EnsureExistingSecureDirectory(paths.ArtifactDirectory, "Recording artifact directory");
            EnsureExistingSecureDirectory(paths.DiagnosticsDirectory, "Recording diagnostics directory");
            if (File.Exists(paths.PartialOutputPath.Value)
                || Directory.Exists(paths.PartialOutputPath.Value))
            {
                if (knownArtifact is not null)
                {
                    var existingArtifact = await VerifyExistingArtifactAsync(
                            paths,
                            paths.PartialOutputPath,
                            knownArtifact,
                            GameViewRecordingArtifactKinds.PartialOutput,
                            GameViewRecordingArtifactMediaTypes.Binary,
                            ValidateReadableToEndAsync,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return GameViewRecordingPartialOutputRecoveryResult.Published(existingArtifact);
                }

                EnsureExistingSecureDirectory(paths.ProviderWorkDirectory, "Recording provider work directory");
                using var recoveryProviderOutput = OpenProviderOutput(paths, cancellationToken);
                var providerMeasurement = await recoveryProviderOutput
                    .MeasureAsync(cancellationToken)
                    .ConfigureAwait(false);
                await RemoveUncheckpointedArtifactAsync(
                        paths,
                        paths.PartialOutputPath,
                        ValidateReadableToEndAsync,
                        providerMeasurement,
                        cancellationToken)
                    .ConfigureAwait(false);
                recoveryProviderOutput.EnsureStillBound();
            }
            if (knownArtifact is not null)
            {
                throw new IOException("Known partial recording artifact is missing from its immutable path.");
            }

            if (!Directory.Exists(paths.ProviderWorkDirectory.Value)
                && !File.Exists(paths.ProviderWorkDirectory.Value))
            {
                return GameViewRecordingPartialOutputRecoveryResult.Absent();
            }

            EnsureExistingSecureDirectory(paths.ProviderWorkDirectory, "Recording provider work directory");
            if (!File.Exists(paths.ProviderOutputPath.Value)
                && !Directory.Exists(paths.ProviderOutputPath.Value))
            {
                return GameViewRecordingPartialOutputRecoveryResult.Absent();
            }

            using var providerOutput = OpenProviderOutput(paths, cancellationToken);
            var artifact = await artifactPublisher.PublishAsync(
                    GameViewRecordingArtifactKinds.PartialOutput,
                    GameViewRecordingArtifactMediaTypes.Binary,
                    ContainedPath.Create(paths.RepositoryRoot, paths.PartialOutputPath),
                    (destination, token) => CopyProviderOutputAsync(
                        providerOutput,
                        destination,
                        token),
                    ValidateReadableToEndAsync,
                    cancellationToken)
                .ConfigureAwait(false);
            providerOutput.EnsureStillBound();
            return GameViewRecordingPartialOutputRecoveryResult.Published(artifact);
        }
        catch (Exception exception) when (IsArtifactFailure(exception))
        {
            return GameViewRecordingPartialOutputRecoveryResult.Failure(
                ExecutionError.InternalError(
                    $"Failed to inspect and publish partial recording output. {exception.Message}",
                    GameViewRecordingErrorCodes.FinalizationFailed));
        }
    }

    private static GameViewRecordingStagingCleanupResult CleanupProviderOutput (
        RecordingArtifactPaths paths)
    {
        try
        {
            EnsureExpectedLayout(paths);
            if (!Directory.Exists(paths.ProviderWorkDirectory.Value)
                && !File.Exists(paths.ProviderWorkDirectory.Value))
            {
                return GameViewRecordingStagingCleanupResult.Success();
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(paths.ProviderWorkDirectory);
            var entries = Directory.EnumerateFileSystemEntries(paths.ProviderWorkDirectory.Value).ToArray();
            if (entries.Length == 0)
            {
                Directory.Delete(paths.ProviderWorkDirectory.Value);
                return GameViewRecordingStagingCleanupResult.Success();
            }

            if (entries.Length != 1
                || !AbsolutePath.Parse(entries[0]).IsSameAs(paths.ProviderOutputPath))
            {
                throw new IOException(
                    "Provider work directory contains entries not owned by the recording output cleanup.");
            }

            using (var providerOutput = OpenProviderOutput(paths, CancellationToken.None))
            {
                providerOutput.EnsureStillBound();
                File.Delete(FileSystemNativePathText.FromGuardedPath(paths.ProviderOutputPath));
            }

            if (File.Exists(paths.ProviderOutputPath.Value)
                || Directory.Exists(paths.ProviderOutputPath.Value))
            {
                throw new IOException("Provider output path still exists after deletion.");
            }

            if (Directory.EnumerateFileSystemEntries(paths.ProviderWorkDirectory.Value).Any())
            {
                throw new IOException(
                    "Provider work directory changed while the known output was being deleted.");
            }

            Directory.Delete(paths.ProviderWorkDirectory.Value);
            return GameViewRecordingStagingCleanupResult.Success();
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            return GameViewRecordingStagingCleanupResult.Failure(
                ExecutionError.InternalError(
                    $"Failed to clean up the provider recording output. {exception.Message}",
                    GameViewRecordingErrorCodes.CleanupFailed));
        }
    }

    private static async ValueTask<GameViewRecordingArtifactDiscardResult> DiscardUnregisteredArtifactsAsync (
        RecordingArtifactPaths paths,
        PathArtifactRef requestArtifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requestArtifact);
        try
        {
            EnsureExpectedLayout(paths);
            EnsureExistingSecureDirectory(paths.ArtifactDirectory, "Recording artifact directory");
            EnsureExistingSecureDirectory(paths.DiagnosticsDirectory, "Recording diagnostics directory");
            var requestPath = ContainedPath.Create(paths.RepositoryRoot, paths.RequestPath);
            EnsureArtifactReferenceIdentifiesDestination(
                requestPath,
                requestArtifact,
                GameViewRecordingArtifactKinds.Request,
                GameViewRecordingArtifactMediaTypes.Json);
            EnsureOnlyDiscardableWorkLayoutExists(paths);

            using (var requestSession = ImmutableArtifactFileReadBoundary.OpenSession(
                requestPath,
                "Unregistered GameView recording request artifact",
                cancellationToken))
            {
                var measurement = await requestSession.MeasureAsync(cancellationToken).ConfigureAwait(false);
                if (measurement.Digest != requestArtifact.Digest
                    || measurement.SizeBytes != requestArtifact.SizeBytes)
                {
                    throw new InvalidDataException(
                        "Unregistered request artifact bytes do not match the lease-owned reference.");
                }

                EnsureOnlyUnregisteredRequestLayoutExists(paths);
                requestSession.EnsureStillBound();
                File.Delete(FileSystemNativePathText.FromGuardedPath(paths.RequestPath));
            }

            if (File.Exists(paths.RequestPath.Value) || Directory.Exists(paths.RequestPath.Value))
            {
                throw new IOException("Unregistered request artifact still exists after deletion.");
            }

            EnsureDirectoryIsEmpty(paths.DiagnosticsDirectory, "Recording diagnostics directory");
            Directory.Delete(paths.DiagnosticsDirectory.Value);
            EnsureDirectoryIsEmpty(paths.ArtifactDirectory, "Recording artifact directory");
            Directory.Delete(paths.ArtifactDirectory.Value);
            DeleteDiscardableWorkLayout(paths);
            return GameViewRecordingArtifactDiscardResult.Success();
        }
        catch (Exception exception) when (IsArtifactFailure(exception))
        {
            return GameViewRecordingArtifactDiscardResult.Failure(
                ExecutionError.InternalError(
                    $"Failed to discard the unregistered recording request artifact. {exception.Message}",
                    GameViewRecordingErrorCodes.CleanupFailed));
        }
    }

    private async ValueTask<GameViewRecordingArtifactPublicationResult> PublishJsonAsync<T> (
        RecordingArtifactPaths paths,
        T value,
        byte[] canonicalBytes,
        ArtifactKind kind,
        AbsolutePath destination,
        PathArtifactRef? knownArtifact,
        CancellationToken cancellationToken)
        where T : notnull
    {
        EnsureExpectedLayout(paths);
        EnsureExistingSecureDirectory(paths.ArtifactDirectory, "Recording artifact directory");
        if (File.Exists(destination.Value) || Directory.Exists(destination.Value))
        {
            if (knownArtifact is not null)
            {
                var existingArtifact = await VerifyExistingArtifactAsync(
                        paths,
                        destination,
                        knownArtifact,
                        kind,
                        GameViewRecordingArtifactMediaTypes.Json,
                        (stream, token) => ValidateCanonicalJsonAsync(
                            stream,
                            canonicalBytes,
                            token),
                        cancellationToken)
                    .ConfigureAwait(false);
                return GameViewRecordingArtifactPublicationResult.Success(existingArtifact);
            }

            await RemoveUncheckpointedArtifactAsync(
                    paths,
                    destination,
                    (stream, token) => ValidateCanonicalJsonAsync(
                        stream,
                        canonicalBytes,
                        token),
                    expectedMeasurement: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        if (knownArtifact is not null)
        {
            throw new IOException("Known recording JSON artifact is missing from its immutable path.");
        }

        var artifact = await artifactPublisher.PublishAsync(
                kind,
                GameViewRecordingArtifactMediaTypes.Json,
                ContainedPath.Create(paths.RepositoryRoot, destination),
                (stream, token) => stream.WriteAsync(canonicalBytes, token),
                (stream, token) => ValidateCanonicalJsonAsync(
                    stream,
                    canonicalBytes,
                    token),
                cancellationToken)
            .ConfigureAwait(false);
        return GameViewRecordingArtifactPublicationResult.Success(artifact);
    }

    private static async ValueTask<PathArtifactRef> VerifyExistingArtifactAsync (
        RecordingArtifactPaths paths,
        AbsolutePath destination,
        PathArtifactRef knownArtifact,
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        Func<Stream, CancellationToken, ValueTask> validateAsync,
        CancellationToken cancellationToken)
    {
        var destinationPath = ContainedPath.Create(paths.RepositoryRoot, destination);
        EnsureArtifactReferenceIdentifiesDestination(
            destinationPath,
            knownArtifact,
            kind,
            mediaType);

        using var session = ImmutableArtifactFileReadBoundary.OpenSession(
            destinationPath,
            "Existing GameView recording artifact",
            cancellationToken);
        var before = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
        if (before.Digest != knownArtifact.Digest
            || before.SizeBytes != knownArtifact.SizeBytes)
        {
            throw new InvalidDataException(
                "Existing recording artifact bytes do not match the durable artifact reference.");
        }

        await session.ValidateAsync(validateAsync, cancellationToken).ConfigureAwait(false);
        var after = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
        before.EnsureMatches(
            after,
            destination,
            "Existing recording artifact changed during recovery validation");
        return knownArtifact;
    }

    private static async ValueTask RemoveUncheckpointedArtifactAsync (
        RecordingArtifactPaths paths,
        AbsolutePath destination,
        Func<Stream, CancellationToken, ValueTask> validateAsync,
        ImmutableArtifactFileReadBoundary.Measurement? expectedMeasurement,
        CancellationToken cancellationToken)
    {
        var destinationPath = ContainedPath.Create(paths.RepositoryRoot, destination);
        using (var session = ImmutableArtifactFileReadBoundary.OpenSession(
            destinationPath,
            "Uncheckpointed GameView recording artifact",
            cancellationToken))
        {
            var before = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
            if (expectedMeasurement is { } expected
                && before != expected)
            {
                throw new InvalidDataException(
                    "Uncheckpointed recording artifact bytes do not match the held provider output.");
            }

            await session.ValidateAsync(validateAsync, cancellationToken).ConfigureAwait(false);
            var after = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
            before.EnsureMatches(
                after,
                destination,
                "Uncheckpointed recording artifact changed during recovery validation");
            session.EnsureStillBound();
            File.Delete(FileSystemNativePathText.FromGuardedPath(destination));
        }

        if (File.Exists(destination.Value) || Directory.Exists(destination.Value))
        {
            throw new IOException(
                "Uncheckpointed recording artifact still exists after recovery deletion.");
        }
    }

    private static void EnsureArtifactReferenceIdentifiesDestination (
        ContainedPath destinationPath,
        PathArtifactRef artifact,
        ArtifactKind kind,
        ArtifactMediaType mediaType)
    {
        if (!UcliPortablePathAdapter.TryFormat(destinationPath.RelativePath, out var portablePath)
            || artifact.Kind != kind
            || artifact.MediaType != mediaType
            || !string.Equals(artifact.Path.Value, portablePath, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Known recording artifact reference does not identify the expected immutable destination.");
        }
    }

    private static void EnsureOnlyUnregisteredRequestLayoutExists (RecordingArtifactPaths paths)
    {
        var entries = Directory.EnumerateFileSystemEntries(paths.ArtifactDirectory.Value)
            .Select(AbsolutePath.Parse)
            .ToArray();
        if (entries.Length != 2
            || !entries.Any(path => path.IsSameAs(paths.RequestPath))
            || !entries.Any(path => path.IsSameAs(paths.DiagnosticsDirectory)))
        {
            throw new IOException(
                "Recording artifact directory contains entries beyond the unregistered request layout.");
        }

        EnsureDirectoryIsEmpty(paths.DiagnosticsDirectory, "Recording diagnostics directory");
    }

    private static void EnsureOnlyDiscardableWorkLayoutExists (RecordingArtifactPaths paths)
    {
        EnsureExistingSecureDirectory(paths.ExecutionWorkDirectory, "Recording work directory");
        EnsureExistingSecureDirectory(paths.ProviderWorkDirectory, "Recording provider work directory");
        EnsureDirectoryIsEmpty(paths.ProviderWorkDirectory, "Recording provider work directory");
        if (File.Exists(paths.ExecutionStatePath.Value)
            || Directory.Exists(paths.ExecutionStatePath.Value))
        {
            throw new IOException(
                "Registered recording execution state must be removed before its artifacts are discarded.");
        }

        var entries = Directory.EnumerateFileSystemEntries(paths.ExecutionWorkDirectory.Value)
            .Select(AbsolutePath.Parse)
            .ToArray();
        if (entries.Any(path => !path.IsSameAs(paths.ProviderWorkDirectory)
                && !path.IsSameAs(paths.ExecutionStateLockPath)))
        {
            throw new IOException(
                "Recording work directory contains entries beyond the rejected-start layout.");
        }

        if (File.Exists(paths.ExecutionStateLockPath.Value))
        {
            FileSystemAccessBoundary.EnsureSecureFile(paths.ExecutionStateLockPath);
        }
        else if (Directory.Exists(paths.ExecutionStateLockPath.Value))
        {
            throw new IOException("Recording execution lock path is not a regular file.");
        }
    }

    private static void DeleteDiscardableWorkLayout (RecordingArtifactPaths paths)
    {
        EnsureDirectoryIsEmpty(paths.ProviderWorkDirectory, "Recording provider work directory");
        Directory.Delete(paths.ProviderWorkDirectory.Value);
        if (File.Exists(paths.ExecutionStateLockPath.Value))
        {
            FileSystemAccessBoundary.EnsureSecureFile(paths.ExecutionStateLockPath);
            File.Delete(FileSystemNativePathText.FromGuardedPath(paths.ExecutionStateLockPath));
        }

        EnsureDirectoryIsEmpty(paths.ExecutionWorkDirectory, "Recording work directory");
        Directory.Delete(paths.ExecutionWorkDirectory.Value);
    }

    private static void EnsureDirectoryIsEmpty (AbsolutePath path, string subject)
    {
        EnsureExistingSecureDirectory(path, subject);
        if (Directory.EnumerateFileSystemEntries(path.Value).Any())
        {
            throw new IOException($"{subject} is not empty: {path}");
        }
    }

    private static GameViewRecordingVideoPublication CreateVideoPublication (
        PathArtifactRef artifact,
        GameViewRecordingMp4ValidationResult validation)
    {
        return new GameViewRecordingVideoPublication(
            artifact,
            validation.Codec,
            validation.SampleCount,
            validation.DurationSeconds,
            validation.EffectiveFrameRate);
    }

    private static byte[] CreateCanonicalJsonBytes<T> (T value)
        where T : notnull
    {
        var serialized = JsonSerializer.SerializeToUtf8Bytes(
            value,
            IpcJsonSerializerOptions.StrictPropertyNames);
        return Rfc8785JsonCanonicalizer.Canonicalize(serialized);
    }

    private static async ValueTask ValidateCanonicalJsonAsync (
        Stream stream,
        byte[] expectedCanonicalBytes,
        CancellationToken cancellationToken)
    {
        var actualBytes = await ReadExactBytesAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!actualBytes.AsSpan().SequenceEqual(expectedCanonicalBytes))
        {
            throw new InvalidDataException("Published JSON bytes differ from the canonical source document.");
        }

        var recanonicalized = Rfc8785JsonCanonicalizer.Canonicalize(actualBytes);
        if (!recanonicalized.AsSpan().SequenceEqual(actualBytes))
        {
            throw new InvalidDataException("Published JSON is not in RFC 8785 canonical form.");
        }

    }

    private static async ValueTask<byte[]> ReadExactBytesAsync (
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (stream.Length > int.MaxValue)
        {
            throw new InvalidDataException("Recording JSON artifact exceeds the supported byte length.");
        }

        var bytes = new byte[checked((int)stream.Length)];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = await stream
                .ReadAsync(bytes.AsMemory(offset), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Recording JSON artifact ended before its measured length.");
            }
            offset += read;
        }

        if (await stream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) != 0)
        {
            throw new InvalidDataException("Recording JSON artifact grew during validation.");
        }
        return bytes;
    }

    private static async ValueTask CopyProviderOutputAsync (
        ArtifactPhysicalFileSession providerOutput,
        Stream destination,
        CancellationToken cancellationToken)
    {
        await providerOutput.ValidateAsync(
                async (source, token) =>
                {
                    await source.CopyToAsync(destination, token).ConfigureAwait(false);
                    await destination.FlushAsync(token).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask ValidateReadableToEndAsync (
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        while (await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) != 0)
        {
        }
    }

    private static ArtifactPhysicalFileSession OpenProviderOutput (
        RecordingArtifactPaths paths,
        CancellationToken cancellationToken)
    {
        return ArtifactPhysicalFileSession.Open(
            ArtifactPhysicalFileRequest.Create(
                ContainedPath.Create(paths.RepositoryRoot, paths.ProviderOutputPath),
                "GameView Recorder provider output"),
            cancellationToken);
    }

    private static void EnsureObservedFrameCount (
        GameViewRecordingMp4ValidationResult validation,
        int? observedEncodedFrameCount)
    {
        if (observedEncodedFrameCount.HasValue
            && validation.SampleCount != checked((ulong)observedEncodedFrameCount.Value))
        {
            throw new InvalidDataException(
                $"MP4 sample count does not match the Recorder observation. Expected={observedEncodedFrameCount.Value}, Actual={validation.SampleCount}.");
        }
    }

    private static RecordingArtifactPaths ResolvePaths (
        ResolvedUnityProjectContext unityProject,
        Guid recordingId)
    {
        var repositoryRoot = unityProject.RepositoryRoot;
        return new RecordingArtifactPaths(
            repositoryRoot,
            UcliStoragePathResolver.ResolveLocalDirectoryPath(repositoryRoot),
            UcliStoragePathResolver.ResolveGameViewRecordingArtifactDirectory(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingRequestArtifactPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingManifestArtifactPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingVideoArtifactPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingCleanupArtifactPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingTerminalArtifactPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingDiagnosticsArtifactDirectory(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingPartialOutputArtifactPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingExecutionWorkDirectory(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingExecutionStatePath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingExecutionStateLockPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingProviderWorkDirectory(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId),
            UcliStoragePathResolver.ResolveGameViewRecordingProviderOutputPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                recordingId));
    }

    private static void EnsureExpectedLayout (RecordingArtifactPaths paths)
    {
        EnsureStrictDescendant(paths.RepositoryRoot, paths.LocalStorageDirectory, "Local storage directory");
        EnsureStrictDescendant(paths.LocalStorageDirectory, paths.ArtifactDirectory, "Recording artifact directory");
        EnsureStrictDescendant(paths.LocalStorageDirectory, paths.ExecutionWorkDirectory, "Recording work directory");
        EnsureStrictDescendant(paths.ArtifactDirectory, paths.RequestPath, "Recording request path");
        EnsureStrictDescendant(paths.ArtifactDirectory, paths.ManifestPath, "Recording manifest path");
        EnsureStrictDescendant(paths.ArtifactDirectory, paths.VideoPath, "Recording video path");
        EnsureStrictDescendant(paths.ArtifactDirectory, paths.CleanupPath, "Recording cleanup path");
        EnsureStrictDescendant(paths.ArtifactDirectory, paths.TerminalPath, "Recording terminal path");
        EnsureStrictDescendant(paths.ArtifactDirectory, paths.DiagnosticsDirectory, "Recording diagnostics directory");
        EnsureStrictDescendant(paths.DiagnosticsDirectory, paths.PartialOutputPath, "Recording partial output path");
        EnsureStrictDescendant(paths.ExecutionWorkDirectory, paths.ExecutionStatePath, "Recording execution state path");
        EnsureStrictDescendant(paths.ExecutionWorkDirectory, paths.ExecutionStateLockPath, "Recording execution state lock path");
        EnsureStrictDescendant(paths.ExecutionWorkDirectory, paths.ProviderWorkDirectory, "Recording provider work directory");
        EnsureStrictDescendant(paths.ProviderWorkDirectory, paths.ProviderOutputPath, "Recording provider output path");
    }

    private static void EnsureStrictDescendant (
        AbsolutePath boundary,
        AbsolutePath target,
        string subject)
    {
        var relation = ContainedPath.Create(boundary, target);
        if (relation.RelativePath.IsRoot)
        {
            throw new InvalidOperationException($"{subject} must be below its owned directory.");
        }
    }

    private static void RecoverUnregisteredPreparation (RecordingArtifactPaths paths)
    {
        var orphanedArtifactCandidates = new List<ContainedPath>();
        if (PathExists(paths.ArtifactDirectory))
        {
            EnsureExistingSecureDirectory(paths.ArtifactDirectory, "Recording artifact directory");
            foreach (var entryText in Directory.EnumerateFileSystemEntries(paths.ArtifactDirectory.Value))
            {
                var entry = AbsolutePath.Parse(entryText);
                if (entry.IsSameAs(paths.DiagnosticsDirectory))
                {
                    EnsureDirectoryIsEmpty(paths.DiagnosticsDirectory, "Recording diagnostics directory");
                    continue;
                }
                if (entry.IsSameAs(paths.RequestPath))
                {
                    FileSystemAccessBoundary.EnsureSecureFile(paths.RequestPath);
                    continue;
                }

                var containedEntry = ContainedPath.Create(paths.ArtifactDirectory, entry);
                if (!IsOwnedPublicationCandidate(containedEntry.RelativePath))
                {
                    throw new IOException(
                        "Recording artifact directory contains entries beyond an unregistered request publication.");
                }

                orphanedArtifactCandidates.Add(
                    ContainedPath.Create(paths.RepositoryRoot, containedEntry.Target));
            }
        }

        if (PathExists(paths.ExecutionWorkDirectory))
        {
            EnsureExistingSecureDirectory(paths.ExecutionWorkDirectory, "Recording work directory");
            if (PathExists(paths.ExecutionStatePath))
            {
                throw new IOException(
                    "A registered recording execution cannot be recovered as an unregistered preparation.");
            }

            foreach (var entryText in Directory.EnumerateFileSystemEntries(paths.ExecutionWorkDirectory.Value))
            {
                var entry = AbsolutePath.Parse(entryText);
                if (entry.IsSameAs(paths.ProviderWorkDirectory))
                {
                    EnsureDirectoryIsEmpty(paths.ProviderWorkDirectory, "Recording provider work directory");
                    continue;
                }
                if (entry.IsSameAs(paths.ExecutionStateLockPath))
                {
                    FileSystemAccessBoundary.EnsureSecureFile(paths.ExecutionStateLockPath);
                    continue;
                }

                throw new IOException(
                    "Recording work directory contains entries beyond an unregistered preparation.");
            }
        }

        foreach (var orphanedCandidate in orphanedArtifactCandidates)
        {
            DeleteHeldOwnedPublicationCandidate(orphanedCandidate);
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(paths.ArtifactDirectory);
        FileSystemAccessBoundary.EnsureSecureDirectory(paths.DiagnosticsDirectory);
        FileSystemAccessBoundary.EnsureSecureDirectory(paths.ProviderWorkDirectory);
    }

    private static bool IsOwnedPublicationCandidate (RootRelativePath relativePath)
    {
        return !relativePath.IsRoot
            && relativePath.Value.IndexOf('/') < 0
            && relativePath.Value.StartsWith(
                FileUtilities.AtomicWriteTemporaryFileNamePrefix,
                StringComparison.Ordinal);
    }

    private static void DeleteHeldOwnedPublicationCandidate (ContainedPath candidate)
    {
        using (var session = ImmutableArtifactFileReadBoundary.OpenSession(
            candidate,
            ImmutableArtifactFilePublisher.TemporaryFileSubject,
            CancellationToken.None))
        {
            session.EnsureStillBound();
            File.Delete(FileSystemNativePathText.FromGuardedPath(candidate.Target));
        }

        if (PathExists(candidate.Target))
        {
            throw new IOException(
                "Orphaned recording artifact publication candidate still exists after deletion.");
        }
    }

    private static bool PathExists (AbsolutePath path) =>
        File.Exists(path.Value) || Directory.Exists(path.Value);

    private static void EnsureExistingSecureDirectory (AbsolutePath path, string subject)
    {
        if (!Directory.Exists(path.Value))
        {
            throw new DirectoryNotFoundException($"{subject} was not found: {path}");
        }

        var attributes = File.GetAttributes(path.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{subject} must not be a reparse point: {path}");
        }
        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"{subject} is not a directory: {path}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(path);
    }

    private static void TryDeleteKnownEmptyPreparationDirectories (RecordingArtifactPaths paths)
    {
        TryDeleteEmptyDirectory(paths.ProviderWorkDirectory);
        TryDeleteEmptyDirectory(paths.ExecutionWorkDirectory);
        TryDeleteEmptyDirectory(paths.DiagnosticsDirectory);
        TryDeleteEmptyDirectory(paths.ArtifactDirectory);
    }

    private static void TryDeleteEmptyDirectory (AbsolutePath path)
    {
        try
        {
            if (Directory.Exists(path.Value)
                && (File.GetAttributes(path.Value) & FileAttributes.ReparsePoint) == 0
                && !Directory.EnumerateFileSystemEntries(path.Value).Any())
            {
                Directory.Delete(path.Value);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static GameViewRecordingArtifactPublicationResult PublicationFailure (
        string subject,
        Exception exception)
    {
        return GameViewRecordingArtifactPublicationResult.Failure(
            ExecutionError.InternalError(
                $"Failed to publish the {subject} artifact. {exception.Message}",
                GameViewRecordingErrorCodes.FinalizationFailed));
    }

    private static bool IsArtifactFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or InvalidDataException
            or JsonException
            or JsonCanonicalizationException;
    }

    private sealed class RecordingArtifactLease : IGameViewRecordingArtifactLease
    {
        private readonly object ownershipGate = new();
        private readonly FileGameViewRecordingArtifactStore store;
        private readonly RecordingArtifactPaths paths;
        private PathArtifactRef? unregisteredRequestArtifact;

        public RecordingArtifactLease (
            FileGameViewRecordingArtifactStore store,
            RecordingArtifactPaths paths)
        {
            this.store = store;
            this.paths = paths;
        }

        public AbsolutePath ExecutionStatePath => paths.ExecutionStatePath;

        public async ValueTask<GameViewRecordingArtifactPublicationResult> PublishRequestAsync (
            GameViewRecordingEffectiveRequest request,
            PathArtifactRef? knownArtifact,
            CancellationToken cancellationToken = default)
        {
            var result = await store
                .PublishRequestAsync(paths, request, knownArtifact, cancellationToken)
                .ConfigureAwait(false);
            if (knownArtifact is null && result.Artifact is { } publishedArtifact)
            {
                lock (ownershipGate)
                {
                    unregisteredRequestArtifact ??= publishedArtifact;
                }
            }

            return result;
        }

        public ValueTask<GameViewRecordingVideoPublicationResult> PublishVideoAsync (
            GameViewRecordingEffectiveRequest request,
            int? observedEncodedFrameCount,
            PathArtifactRef? knownArtifact,
            CancellationToken cancellationToken = default)
        {
            return store.PublishVideoAsync(
                paths,
                request,
                observedEncodedFrameCount,
                knownArtifact,
                cancellationToken);
        }

        public ValueTask<GameViewRecordingArtifactPublicationResult> PublishManifestAsync (
            GameViewRecordingManifest manifest,
            PathArtifactRef? knownArtifact,
            CancellationToken cancellationToken = default)
        {
            return store.PublishManifestAsync(paths, manifest, knownArtifact, cancellationToken);
        }

        public ValueTask<GameViewRecordingArtifactPublicationResult> PublishCleanupAsync (
            GameViewRecordingCleanupRecord cleanup,
            PathArtifactRef? knownArtifact,
            CancellationToken cancellationToken = default)
        {
            return store.PublishCleanupAsync(paths, cleanup, knownArtifact, cancellationToken);
        }

        public ValueTask<GameViewRecordingArtifactPublicationResult> PublishTerminalRecordAsync (
            GameViewRecordingTerminalRecord terminalRecord,
            PathArtifactRef? knownArtifact,
            CancellationToken cancellationToken = default)
        {
            return store.PublishTerminalRecordAsync(paths, terminalRecord, knownArtifact, cancellationToken);
        }

        public ValueTask<GameViewRecordingPartialOutputRecoveryResult> RecoverPartialOutputAsync (
            PathArtifactRef? knownArtifact,
            CancellationToken cancellationToken = default)
        {
            return store.RecoverPartialOutputAsync(paths, knownArtifact, cancellationToken);
        }

        public async ValueTask<GameViewRecordingArtifactDiscardResult> DiscardUnregisteredArtifactsAsync (
            PathArtifactRef requestArtifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(requestArtifact);
            lock (ownershipGate)
            {
                if (unregisteredRequestArtifact != requestArtifact)
                {
                    return GameViewRecordingArtifactDiscardResult.Failure(
                        ExecutionError.InternalError(
                            "The request artifact is not owned as an unregistered publication by this lease.",
                            GameViewRecordingErrorCodes.CleanupFailed));
                }
            }

            var result = await FileGameViewRecordingArtifactStore
                .DiscardUnregisteredArtifactsAsync(paths, requestArtifact, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsSuccess)
            {
                lock (ownershipGate)
                {
                    if (unregisteredRequestArtifact == requestArtifact)
                    {
                        unregisteredRequestArtifact = null;
                    }
                }
            }

            return result;
        }

        public GameViewRecordingStagingCleanupResult CleanupProviderOutput ()
        {
            return FileGameViewRecordingArtifactStore.CleanupProviderOutput(paths);
        }
    }

    private sealed record RecordingArtifactPaths (
        AbsolutePath RepositoryRoot,
        AbsolutePath LocalStorageDirectory,
        AbsolutePath ArtifactDirectory,
        AbsolutePath RequestPath,
        AbsolutePath ManifestPath,
        AbsolutePath VideoPath,
        AbsolutePath CleanupPath,
        AbsolutePath TerminalPath,
        AbsolutePath DiagnosticsDirectory,
        AbsolutePath PartialOutputPath,
        AbsolutePath ExecutionWorkDirectory,
        AbsolutePath ExecutionStatePath,
        AbsolutePath ExecutionStateLockPath,
        AbsolutePath ProviderWorkDirectory,
        AbsolutePath ProviderOutputPath);
}
