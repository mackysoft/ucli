using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Execution.ReadPostcondition;

/// <summary> Atomically persists project-scoped eval-call consumption and read fences. </summary>
public sealed class MutationReadPostconditionJournal
{
    private const int SchemaVersion = 2;
    private static readonly TimeSpan LockAcquireTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new VocabularyJsonConverterFactory() },
    };

    /// <summary> Reads the persisted read fences when the journal is valid. </summary>
    public async ValueTask<MutationReadPostconditionJournalReadResult> ReadOrNullAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(storageRoot, projectFingerprint);
        var readDocument = await TryReadDocumentAsync(documentPath, cancellationToken).ConfigureAwait(false);
        return readDocument.Failure is not null
            ? MutationReadPostconditionJournalReadResult.Failed(readDocument.Failure)
            : MutationReadPostconditionJournalReadResult.Success(ToReadPostcondition(readDocument.Document!));
    }

    /// <summary> Merges mutation read fences without consuming an eval plan token. </summary>
    public async ValueTask<MutationReadPostconditionJournalWriteResult> WriteMergedAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        ExecutionReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default)
    {
        if (readPostcondition is null)
        {
            throw new ArgumentNullException(nameof(readPostcondition));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(storageRoot, projectFingerprint);
        var lockPath = UcliStoragePathResolver.ResolveMutationReadPostconditionLockPath(storageRoot, projectFingerprint);
        try
        {
            using var writeLock = await FileExclusiveLock.AcquireAsync(lockPath, LockAcquireTimeout, cancellationToken).ConfigureAwait(false);
            var readDocument = await TryReadDocumentAsync(documentPath, cancellationToken).ConfigureAwait(false);
            if (readDocument.Failure is not null)
            {
                return MutationReadPostconditionJournalWriteResult.Failed(readDocument.Failure);
            }

            var document = readDocument.Document ?? JournalDocument.Empty;
            var merged = MergeRequirements(document.Requirements.Concat(readPostcondition.Requirements));
            var writeDocument = document with { Requirements = merged };
            await WriteDocumentAsync(documentPath, writeDocument, cancellationToken).ConfigureAwait(false);
            return MutationReadPostconditionJournalWriteResult.Success();
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return MutationReadPostconditionJournalWriteResult.Failed(StorageFailure(documentPath, exception));
        }
        catch (ArgumentException exception)
        {
            return MutationReadPostconditionJournalWriteResult.Failed(InvalidDocumentFailure(documentPath, exception));
        }
    }

    /// <summary> Consumes a verified eval plan token and publishes all broad read fences as one atomic update. </summary>
    public async ValueTask<EvalCallAdmissionResult> TryAdmitEvalCallAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        EvalCallAdmission admission,
        CancellationToken cancellationToken = default)
    {
        if (admission is null)
        {
            throw new ArgumentNullException(nameof(admission));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(storageRoot, projectFingerprint);
        var lockPath = UcliStoragePathResolver.ResolveMutationReadPostconditionLockPath(storageRoot, projectFingerprint);
        try
        {
            using var writeLock = await FileExclusiveLock.AcquireAsync(lockPath, LockAcquireTimeout, cancellationToken).ConfigureAwait(false);
            var readDocument = await TryReadDocumentAsync(documentPath, cancellationToken).ConfigureAwait(false);
            if (readDocument.Failure is not null)
            {
                return EvalCallAdmissionResult.Failed(readDocument.Failure);
            }

            var document = readDocument.Document ?? JournalDocument.Empty;
            if (document.ConsumedEvalCalls.Any(entry => entry.Nonce == admission.Nonce || entry.TokenDigest == admission.TokenDigest.ToString()))
            {
                return EvalCallAdmissionResult.Replay();
            }

            var fenceUtc = CalculateFenceUtc(document.Requirements, DateTimeOffset.UtcNow);
            var readPostcondition = CreateBroadReadPostcondition(fenceUtc);
            var writeDocument = new JournalDocument(
                SchemaVersion,
                MergeRequirements(document.Requirements.Concat(readPostcondition.Requirements)),
                document.ConsumedEvalCalls.Append(ConsumedEvalCall.From(admission)).ToArray());
            await WriteDocumentAsync(documentPath, writeDocument, cancellationToken).ConfigureAwait(false);
            return EvalCallAdmissionResult.Admitted(readPostcondition);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return EvalCallAdmissionResult.Failed(StorageFailure(documentPath, exception));
        }
        catch (ArgumentException exception)
        {
            return EvalCallAdmissionResult.Failed(InvalidDocumentFailure(documentPath, exception));
        }
    }

    private static async ValueTask<DocumentRead> TryReadDocumentAsync (AbsolutePath documentPath, CancellationToken cancellationToken)
    {
        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(documentPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsStorageFailure(exception))
        {
            return DocumentRead.Failed(StorageFailure(documentPath, exception));
        }

        if (json is null)
        {
            return DocumentRead.Succeeded(null);
        }

        try
        {
            var document = JsonSerializer.Deserialize<JournalDocument>(json, SerializerOptions)
                ?? throw new JsonException("Mutation read-postcondition journal JSON is null.");
            ValidateDocument(document);
            return DocumentRead.Succeeded(document with { Requirements = MergeRequirements(document.Requirements) });
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            return DocumentRead.Failed(InvalidDocumentFailure(documentPath, exception));
        }
        catch (Exception exception)
        {
            return DocumentRead.Failed(StorageFailure(documentPath, exception));
        }
    }

    private static async ValueTask WriteDocumentAsync (AbsolutePath documentPath, JournalDocument document, CancellationToken cancellationToken)
    {
        ValidateDocument(document);
        var json = JsonSerializer.Serialize(document, SerializerOptions) + Environment.NewLine;
        await FileUtilities.WriteAllTextAtomicallyAsync(documentPath, json, cancellationToken).ConfigureAwait(false);
    }

    private static ExecutionReadPostcondition? ToReadPostcondition (JournalDocument document)
    {
        return document.Requirements.Count == 0 ? null : new ExecutionReadPostcondition(document.Requirements);
    }

    private static DateTimeOffset CalculateFenceUtc (IReadOnlyList<ExecutionReadPostconditionRequirement> requirements, DateTimeOffset utcNow)
    {
        var maximumRequirementUtc = requirements.Count == 0
            ? DateTimeOffset.MinValue
            : requirements.Max(static requirement => requirement.MinSafeGeneratedAtUtc);
        var nextAfterExisting = maximumRequirementUtc == DateTimeOffset.MaxValue
            ? DateTimeOffset.MaxValue
            : maximumRequirementUtc.AddTicks(1);
        return utcNow > nextAfterExisting ? utcNow : nextAfterExisting;
    }

    private static ExecutionReadPostcondition CreateBroadReadPostcondition (DateTimeOffset fenceUtc)
    {
        return new ExecutionReadPostcondition(
        [
            new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.AssetSearch, fenceUtc, null),
            new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.GuidPath, fenceUtc, null),
            new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.SceneTreeLite, fenceUtc, null),
        ]);
    }

    private static IReadOnlyList<ExecutionReadPostconditionRequirement> MergeRequirements (IEnumerable<ExecutionReadPostconditionRequirement> requirements)
    {
        var merged = new Dictionary<(ExecutionReadPostconditionSurface Surface, UnityScenePath? ScenePath), ExecutionReadPostconditionRequirement>();
        foreach (var requirement in requirements)
        {
            if (requirement is null)
            {
                throw new ArgumentNullException(nameof(requirements));
            }

            var key = (requirement.Surface, requirement.ScenePath);
            if (!merged.TryGetValue(key, out var existing) || requirement.MinSafeGeneratedAtUtc > existing.MinSafeGeneratedAtUtc)
            {
                merged[key] = requirement;
            }
        }

        return merged.OrderBy(static pair => pair.Key.Surface)
            .ThenBy(static pair => pair.Key.ScenePath?.Value, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToArray();
    }

    private static void ValidateDocument (JournalDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (document.SchemaVersion != SchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(document.SchemaVersion), document.SchemaVersion, $"schemaVersion must be {SchemaVersion}.");
        }

        if (document.Requirements is null)
        {
            throw new ArgumentNullException(nameof(document.Requirements));
        }

        if (document.ConsumedEvalCalls is null)
        {
            throw new ArgumentNullException(nameof(document.ConsumedEvalCalls));
        }

        _ = MergeRequirements(document.Requirements);
        var seenNonces = new HashSet<string>(StringComparer.Ordinal);
        var seenTokenDigests = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in document.ConsumedEvalCalls)
        {
            if (entry is null)
            {
                throw new ArgumentNullException(nameof(document.ConsumedEvalCalls));
            }

            entry.Validate();
            if (!seenNonces.Add(entry.Nonce) || !seenTokenDigests.Add(entry.TokenDigest))
            {
                throw new ArgumentException("Consumed eval calls must not reuse a nonce or token digest.", nameof(document));
            }
        }
    }

    private static MutationReadPostconditionJournalFailure InvalidDocumentFailure (AbsolutePath path, Exception exception) => new(
        MutationReadPostconditionJournalFailureKind.InvalidDocument,
        $"Mutation read-postcondition journal is invalid: {path}. {exception.Message}");

    private static MutationReadPostconditionJournalFailure StorageFailure (AbsolutePath path, Exception exception) => new(
        MutationReadPostconditionJournalFailureKind.Storage,
        $"Failed to access mutation read-postcondition journal: {path}. {exception.Message}");

    private static bool IsStorageFailure (Exception exception) => exception is IOException or UnauthorizedAccessException or TimeoutException;

    private sealed record DocumentRead (JournalDocument? Document, MutationReadPostconditionJournalFailure? Failure)
    {
        public static DocumentRead Succeeded (JournalDocument? document) => new(document, null);
        public static DocumentRead Failed (MutationReadPostconditionJournalFailure failure) => new(null, failure);
    }

    private sealed record JournalDocument (
        int SchemaVersion,
        IReadOnlyList<ExecutionReadPostconditionRequirement> Requirements,
        IReadOnlyList<ConsumedEvalCall> ConsumedEvalCalls)
    {
        public static JournalDocument Empty { get; } = new(MutationReadPostconditionJournal.SchemaVersion, [], []);
    }

    private sealed record ConsumedEvalCall (
        string Nonce,
        string TokenDigest,
        Guid RequestId,
        string SourceDigest,
        string ExecutionDigest,
        long EditorGeneration,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc)
    {
        public static ConsumedEvalCall From (EvalCallAdmission admission) => new(
            admission.Nonce,
            admission.TokenDigest.ToString(),
            admission.RequestId,
            admission.SourceDigest.ToString(),
            admission.ExecutionDigest.ToString(),
            admission.EditorGeneration,
            admission.IssuedAtUtc,
            admission.ExpiresAtUtc);

        public void Validate ()
        {
            _ = new EvalCallAdmission(
                Nonce,
                Sha256Digest.Parse(TokenDigest),
                RequestId,
                Sha256Digest.Parse(SourceDigest),
                Sha256Digest.Parse(ExecutionDigest),
                EditorGeneration,
                IssuedAtUtc,
                ExpiresAtUtc);
        }
    }
}
