using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;

internal static class LifecycleExecutionContractTestFactory
{
    internal static readonly Guid ExecutionId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");
    internal static readonly ProjectFingerprint ProjectFingerprint =
        new("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    internal static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset DeadlineUtc =
        StartedAtUtc.AddMinutes(5);
    internal static readonly UnityProjectIdentity Project =
        new("/workspace/UnityProject", ProjectFingerprint, "6000.1.4f1");
    internal static readonly LifecycleExecutionHostRegistration Host =
        new(
            new ProcessIdentity(4200, 123456),
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("10000000-0000-0000-0000-000000000002"));
    internal static readonly UnityEditorGenerationSnapshot StartedGeneration =
        new(10, 20, 30, 40);
    internal static readonly UnityEditorGenerationSnapshot TerminalGeneration =
        new(11, 21, 31, 41);

    internal static LifecycleExecutionStartBinding CreateStart (
        LifecycleExecutionKind kind,
        ExecutionLifecycle lifecycle = ExecutionLifecycle.Active,
        LifecycleExecutionState? state = null)
    {
        var actualState = state ?? lifecycle switch
        {
            ExecutionLifecycle.Active => LifecycleExecutionState.Registered,
            ExecutionLifecycle.Recovery => LifecycleExecutionState.Recovering,
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle)),
        };
        return new LifecycleExecutionStartBinding(
            CreateReference(kind, lifecycle, actualState),
            Project,
            Host,
            StartedGeneration,
            DeadlineUtc,
            StartedAtUtc);
    }

    internal static ExecutionRef CreateReference (
        LifecycleExecutionKind kind,
        ExecutionLifecycle lifecycle,
        LifecycleExecutionState state)
    {
        var definition = new LifecycleExecutionDefinition(kind);
        var executionKind = definition.ExecutionKind;
        var executionState = new ExecutionState(TextVocabulary.GetText(state));
        var digest = LifecycleExecutionDefinitionDigest.Calculate(definition);
        var locator = new ExecutionStatusLocator(
            $"lifecycle-executions/{ExecutionId:N}/status.json");
        return lifecycle switch
        {
            ExecutionLifecycle.Active => new ActiveExecutionRef(
                executionKind,
                ExecutionId,
                digest,
                executionState,
                locator),
            ExecutionLifecycle.Recovery => new RecoveryExecutionRef(
                executionKind,
                ExecutionId,
                digest,
                executionState,
                locator),
            ExecutionLifecycle.Terminal => new TerminalExecutionRef(
                executionKind,
                ExecutionId,
                digest,
                executionState,
                statusLocator: null,
                CreateTerminalArtifactRef()),
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle)),
        };
    }

    internal static RefreshLifecycleResult CreateRefreshResult ()
    {
        return new RefreshLifecycleResult(
            new RefreshLifecycleResult.RefreshEvidence(
                StartedAtUtc.AddSeconds(1),
                StartedAtUtc.AddSeconds(3),
                domainReloadGenerationBefore: 20,
                domainReloadGenerationAfter: 21),
            CreateObservation(
                TerminalGeneration,
                UnityEditorPlayModeState.Stopped,
                UnityEditorPlayModeTransition.None),
            readPostcondition: null);
    }

    internal static PlayLifecycleTransitionResult CreatePlayResult (
        PlayLifecycleTransitionCommand transition,
        bool successful = true)
    {
        var beforeState = transition == PlayLifecycleTransitionCommand.Enter
            ? UnityEditorPlayModeState.Stopped
            : UnityEditorPlayModeState.Playing;
        var afterState = transition == PlayLifecycleTransitionCommand.Enter
            ? UnityEditorPlayModeState.Playing
            : UnityEditorPlayModeState.Stopped;
        var before = CreateObservation(
            StartedGeneration,
            beforeState,
            UnityEditorPlayModeTransition.None);
        var after = CreateObservation(
            TerminalGeneration,
            afterState,
            UnityEditorPlayModeTransition.None);
        if (successful)
        {
            return new PlayLifecycleTransitionResult(
                transition,
                transition == PlayLifecycleTransitionCommand.Enter
                    ? PlayLifecycleTransitionOutcome.Entered
                    : PlayLifecycleTransitionOutcome.Exited,
                before,
                after,
                Observed: null,
                ApplicationState: null);
        }

        return new PlayLifecycleTransitionResult(
            transition,
            PlayLifecycleTransitionOutcome.Blocked,
            before,
            After: null,
            Observed: before,
            ApplicationState: ExecutionApplicationState.NotApplied);
    }

    internal static CompileLifecycleResult CreateCompileResult (int errorCount = 0)
    {
        return new CompileLifecycleResult(
            new CompileLifecycleResult.RefreshEvidence(
                CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc.AddSeconds(1),
                CompletedAtUtc: StartedAtUtc.AddSeconds(2),
                Completed: true),
            new CompileLifecycleResult.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 10,
                CompileGenerationAfter: 11,
                new CompileLifecycleResult.DiagnosticsEvidence(
                    ErrorCount: errorCount,
                    WarningCount: 0,
                    PrimaryDiagnostic: null)),
            new CompileLifecycleResult.DomainReloadEvidence(
                ReloadRequired: true,
                ReloadObserved: true,
                GenerationBefore: 20,
                GenerationAfter: 21,
                Settled: true),
            new CompileLifecycleResult.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                State: CreateState(
                    TerminalGeneration,
                    UnityEditorPlayModeState.Stopped,
                    UnityEditorPlayModeTransition.None),
                ObservedAtUtc: StartedAtUtc.AddSeconds(4),
                ActionRequired: null,
                PrimaryDiagnostic: null));
    }

    internal static UnityEditorObservation CreateObservation (
        UnityEditorGenerationSnapshot generations,
        UnityEditorPlayModeState playModeState,
        UnityEditorPlayModeTransition transition)
    {
        return new UnityEditorObservation(
            "0.5.0",
            "6000.1.4f1",
            ProjectFingerprint,
            CreateState(generations, playModeState, transition),
            StartedAtUtc.AddSeconds(4),
            actionRequired: null,
            primaryDiagnostic: null);
    }

    private static UnityEditorStateSnapshot CreateState (
        UnityEditorGenerationSnapshot generations,
        UnityEditorPlayModeState playModeState,
        UnityEditorPlayModeTransition transition)
    {
        return new UnityEditorStateSnapshot(
            UnityEditorMode.Gui,
            UnityEditorLifecycleState.Ready,
            UnityEditorCompileState.Ready,
            generations,
            new UnityEditorPlayModeSnapshot(
                playModeState,
                transition,
                IsPlaying: playModeState == UnityEditorPlayModeState.Playing,
                IsPlayingOrWillChangePlaymode:
                    playModeState == UnityEditorPlayModeState.Playing
                    || transition == UnityEditorPlayModeTransition.Entering));
    }

    private static ArtifactRef CreateTerminalArtifactRef ()
    {
        return new PathArtifactRef(
            LifecycleExecutionArtifactContract.TerminalRecordKind,
            LifecycleExecutionArtifactContract.TerminalRecordMediaType,
            new ArtifactPath($"lifecycle-executions/{ExecutionId:N}/terminal-record.json"),
            Sha256Digest.Parse(new string('f', 64)),
            sizeBytes: 512,
            StartedAtUtc.AddSeconds(5));
    }
}
