using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Records one state restoration owned by a recording execution.</summary>
public sealed record GameViewRecordingStateRestoration
{
    [JsonConstructor]
    public GameViewRecordingStateRestoration (
        GameViewRecordingStateRestorationKind kind,
        string? beforeValue,
        string? afterValue,
        bool changed,
        bool restoreAttempted,
        GameViewRecordingStateRestorationDisposition disposition,
        UcliCode? reasonCode)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "State restoration kind must be defined.");
        }
        if (!TextVocabulary.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "State restoration disposition must be defined.");
        }
        if (disposition == GameViewRecordingStateRestorationDisposition.Unchanged
            && (changed || restoreAttempted || reasonCode is not null))
        {
            throw new ArgumentException("An unchanged state was neither changed nor restored and has no failure reason.");
        }
        if (disposition == GameViewRecordingStateRestorationDisposition.Restored
            && (!changed || !restoreAttempted || reasonCode is not null))
        {
            throw new ArgumentException("A restored state must have been changed and restored without a failure reason.");
        }
        if (disposition == GameViewRecordingStateRestorationDisposition.Failed
            && (!restoreAttempted || reasonCode is null))
        {
            throw new ArgumentException("A failed state restoration requires an attempted restoration and reason code.");
        }

        Kind = kind;
        BeforeValue = beforeValue;
        AfterValue = afterValue;
        Changed = changed;
        RestoreAttempted = restoreAttempted;
        Disposition = disposition;
        ReasonCode = reasonCode;
    }

    public GameViewRecordingStateRestorationKind Kind { get; }

    public string? BeforeValue { get; }

    public string? AfterValue { get; }

    public bool Changed { get; }

    public bool RestoreAttempted { get; }

    public GameViewRecordingStateRestorationDisposition Disposition { get; }

    public UcliCode? ReasonCode { get; }
}

/// <summary>Records one resource release owned by a recording execution.</summary>
public sealed record GameViewRecordingResourceRelease
{
    [JsonConstructor]
    public GameViewRecordingResourceRelease (
        GameViewRecordingResourceKind kind,
        bool acquired,
        bool releaseAttempted,
        GameViewRecordingResourceReleaseDisposition disposition,
        UcliCode? reasonCode)
    {
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Recording resource kind must be defined.");
        }
        if (!TextVocabulary.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "Resource release disposition must be defined.");
        }
        if (disposition == GameViewRecordingResourceReleaseDisposition.NotAcquired
            && (acquired || releaseAttempted || reasonCode is not null))
        {
            throw new ArgumentException("A resource that was not acquired was not released and has no failure reason.");
        }
        if (disposition == GameViewRecordingResourceReleaseDisposition.Released
            && (!acquired || !releaseAttempted || reasonCode is not null))
        {
            throw new ArgumentException("A released resource must have been acquired and released without a failure reason.");
        }
        if (disposition == GameViewRecordingResourceReleaseDisposition.Failed
            && (!releaseAttempted || reasonCode is null))
        {
            throw new ArgumentException("A failed resource release requires an attempted release and reason code.");
        }

        Kind = kind;
        Acquired = acquired;
        ReleaseAttempted = releaseAttempted;
        Disposition = disposition;
        ReasonCode = reasonCode;
    }

    public GameViewRecordingResourceKind Kind { get; }

    public bool Acquired { get; }

    public bool ReleaseAttempted { get; }

    public GameViewRecordingResourceReleaseDisposition Disposition { get; }

    public UcliCode? ReasonCode { get; }
}

/// <summary>Represents the immutable recording cleanup artifact.</summary>
public sealed record GameViewRecordingCleanupRecord
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public GameViewRecordingCleanupRecord (
        int schemaVersion,
        Guid recordingId,
        Sha256Digest requestDigest,
        IReadOnlyList<GameViewRecordingStateRestoration> stateRestorations,
        IReadOnlyList<GameViewRecordingResourceRelease> resourceReleases,
        GameViewRecordingCleanupDisposition disposition,
        DateTimeOffset completedAtUtc)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Cleanup schema version must be one.");
        }
        if (!TextVocabulary.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition), disposition, "Cleanup disposition must be defined.");
        }

        var restorations = ContractArgumentGuard.RequireItems(stateRestorations, nameof(stateRestorations));
        var releases = ContractArgumentGuard.RequireItems(resourceReleases, nameof(resourceReleases));
        EnsureExactKinds(
            restorations.Select(static item => item.Kind),
            GetAllValues<GameViewRecordingStateRestorationKind>(),
            nameof(stateRestorations));
        EnsureExactKinds(
            releases.Select(static item => item.Kind),
            GetAllValues<GameViewRecordingResourceKind>(),
            nameof(resourceReleases));

        var expectedDisposition = ResolveDisposition(restorations, releases);
        if (disposition != expectedDisposition)
        {
            throw new ArgumentException("Cleanup disposition must equal the aggregate of every restoration and release.", nameof(disposition));
        }

        SchemaVersion = schemaVersion;
        RecordingId = ContractArgumentGuard.RequireNonEmptyGuid(recordingId, nameof(recordingId));
        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        StateRestorations = restorations;
        ResourceReleases = releases;
        Disposition = disposition;
        CompletedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(completedAtUtc, nameof(completedAtUtc));
    }

    public int SchemaVersion { get; }

    public Guid RecordingId { get; }

    public Sha256Digest RequestDigest { get; }

    public IReadOnlyList<GameViewRecordingStateRestoration> StateRestorations { get; }

    public IReadOnlyList<GameViewRecordingResourceRelease> ResourceReleases { get; }

    public GameViewRecordingCleanupDisposition Disposition { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    private static GameViewRecordingCleanupDisposition ResolveDisposition (
        IReadOnlyList<GameViewRecordingStateRestoration> restorations,
        IReadOnlyList<GameViewRecordingResourceRelease> releases)
    {
        if (restorations.Any(static item => item.Disposition == GameViewRecordingStateRestorationDisposition.Unconfirmed)
            || releases.Any(static item => item.Disposition == GameViewRecordingResourceReleaseDisposition.Unconfirmed))
        {
            return GameViewRecordingCleanupDisposition.Unconfirmed;
        }
        if (restorations.Any(static item => item.Disposition == GameViewRecordingStateRestorationDisposition.Failed)
            || releases.Any(static item => item.Disposition == GameViewRecordingResourceReleaseDisposition.Failed))
        {
            return GameViewRecordingCleanupDisposition.Failed;
        }

        return GameViewRecordingCleanupDisposition.Complete;
    }

    private static T[] GetAllValues<T> () where T : struct, Enum =>
        (T[])Enum.GetValues(typeof(T));

    private static void EnsureExactKinds<T> (
        IEnumerable<T> actual,
        IReadOnlyCollection<T> expected,
        string parameterName)
        where T : struct, Enum
    {
        var values = actual.ToArray();
        if (values.Length != expected.Count
            || values.Distinct().Count() != expected.Count
            || expected.Any(value => !values.Contains(value)))
        {
            throw new ArgumentException("Cleanup entries must contain every required kind exactly once.", parameterName);
        }
    }
}
