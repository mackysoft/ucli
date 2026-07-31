using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> endpoint-registered lifecycle progress payload. </summary>
public sealed record DaemonStartLifecycleSnapshotProgressEntry
{
    /// <summary> Initializes one validated endpoint-registered lifecycle progress payload. </summary>
    /// <exception cref="ArgumentException"> Thrown when the lifecycle tuple is inconsistent. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="ProjectFingerprint" /> or <paramref name="Generations" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="TimeoutMilliseconds" /> is negative, a finite contract value is undefined, or <paramref name="PayloadKind" /> does not identify a lifecycle snapshot. </exception>
    [JsonConstructor]
    public DaemonStartLifecycleSnapshotProgressEntry (
        DaemonStartProgressPayloadKind PayloadKind,
        ProjectFingerprint ProjectFingerprint,
        int TimeoutMilliseconds,
        UnityEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        UnityEditorLifecycleState LifecycleState,
        UnityEditorBlockingReason? BlockingReason,
        UnityEditorGenerationSnapshot Generations,
        bool CanAcceptExecutionRequests)
    {
        if (PayloadKind != DaemonStartProgressPayloadKind.LifecycleSnapshot)
        {
            throw new ArgumentOutOfRangeException(nameof(PayloadKind), PayloadKind, "Payload kind must identify a lifecycle snapshot.");
        }

        if (EditorMode.HasValue && !TextVocabulary.IsDefined(EditorMode.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(EditorMode), EditorMode, "Editor mode must be defined when specified.");
        }

        if (!TextVocabulary.IsDefined(OnStartupBlocked))
        {
            throw new ArgumentOutOfRangeException(nameof(OnStartupBlocked), OnStartupBlocked, "Startup-blocked process policy must be defined.");
        }

        if (!UnityEditorLifecycleSemantics.IsConsistent(LifecycleState, BlockingReason, CanAcceptExecutionRequests))
        {
            throw new ArgumentException("Lifecycle state, blocking reason, and request acceptance must form a consistent tuple.", nameof(LifecycleState));
        }

        this.PayloadKind = PayloadKind;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
        this.EditorMode = EditorMode;
        this.OnStartupBlocked = OnStartupBlocked;
        this.LifecycleState = LifecycleState;
        this.BlockingReason = BlockingReason;
        this.Generations = ContractArgumentGuard.RequireNotNull(Generations, nameof(Generations));
        this.CanAcceptExecutionRequests = CanAcceptExecutionRequests;
    }

    public DaemonStartProgressPayloadKind PayloadKind { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public int TimeoutMilliseconds { get; }

    public UnityEditorMode? EditorMode { get; }

    public DaemonStartupBlockedProcessPolicy OnStartupBlocked { get; }

    public UnityEditorLifecycleState LifecycleState { get; }

    public UnityEditorBlockingReason? BlockingReason { get; }

    public UnityEditorGenerationSnapshot Generations { get; }

    public bool CanAcceptExecutionRequests { get; }
}
