using System.Globalization;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests;

internal static class PlayCommandOutputTestData
{
    public static string ProjectPath { get; } = ProjectPathTestValues.RepositoryUnityProject;

    public const string ServerVersion = "0.5.0";

    public const string UnityVersion = "6000.1.4f1";

    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static UnityEditorCompileState CompileState { get; } = UnityEditorCompileState.Ready;

    public const long CompileGeneration = 12;

    public const long DomainReloadGeneration = 7;

    public static readonly DateTimeOffset ObservedAtUtc =
        DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture);

    public static ProjectIdentityInfo CreateProject ()
    {
        return ProjectIdentityInfoTestFactory.CreateWithProjectPath(projectPath: ProjectPath);
    }

    public static TerminalExecutionRef CreateTerminalExecutionReference (
        LifecycleExecutionKind kind,
        LifecycleExecutionState state = LifecycleExecutionState.Completed)
    {
        var definition = new LifecycleExecutionDefinition(kind);
        var executionId = kind == LifecycleExecutionKind.PlayEnter
            ? Guid.Parse("8d816e63-b50a-4135-8f63-c89b48dc0d8a")
            : Guid.Parse("99fb4266-a116-4944-84f9-3412b3783035");
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(TextVocabulary.GetText(state)),
            statusLocator: null,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath(
                    $".ucli/local/artifacts/lifecycle-execution/{kind}/{executionId:N}/terminal.json"),
                Sha256Digest.Parse(new string('a', 64)),
                sizeBytes: 256,
            ObservedAtUtc));
    }

    public static ExecutionRef CreateReconnectableExecutionReference (
        LifecycleExecutionKind kind,
        ExecutionLifecycle lifecycle)
    {
        var definition = new LifecycleExecutionDefinition(kind);
        var executionId = kind == LifecycleExecutionKind.PlayEnter
            ? Guid.Parse("8d816e63-b50a-4135-8f63-c89b48dc0d8a")
            : Guid.Parse("99fb4266-a116-4944-84f9-3412b3783035");
        var statusLocator = new ExecutionStatusLocator(
            $".ucli/local/state/lifecycle-execution/{kind}/{executionId:N}/start.json");
        return lifecycle switch
        {
            ExecutionLifecycle.Active => new ActiveExecutionRef(
                definition.ExecutionKind,
                executionId,
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                new ExecutionState(TextVocabulary.GetText(
                    LifecycleExecutionState.Registered)),
                statusLocator),
            ExecutionLifecycle.Recovery => new RecoveryExecutionRef(
                definition.ExecutionKind,
                executionId,
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                new ExecutionState(TextVocabulary.GetText(
                    LifecycleExecutionState.Publishing)),
                statusLocator),
            _ => throw new ArgumentOutOfRangeException(
                nameof(lifecycle),
                lifecycle,
                "A reconnectable reference must be active or recovery."),
        };
    }

    public static UnityEditorObservation CreateLifecycleSnapshot (
        UnityEditorLifecycleState lifecycleState,
        UnityEditorPlayModeSnapshot playMode,
        long playModeGeneration)
    {
        var state = new UnityEditorStateSnapshot(
            UnityEditorMode.Gui,
            lifecycleState,
            CompileState,
            new UnityEditorGenerationSnapshot(
                CompileGeneration,
                DomainReloadGeneration,
                AssetRefreshGeneration: 0,
                PlayModeGeneration: playModeGeneration),
            playMode);
        return new UnityEditorObservation(
            ServerVersion,
            UnityVersion,
            ProjectFingerprint,
            state,
            ObservedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null);
    }

    public static PlayLifecycleSnapshotOutput CreateLifecycleSnapshotOutput (UnityEditorObservation snapshot)
    {
        var state = snapshot.State;
        return new PlayLifecycleSnapshotOutput(
            ServerVersion: snapshot.ServerVersion,
            EditorMode: state.EditorMode,
            UnityVersion: snapshot.UnityVersion,
            ProjectFingerprint: snapshot.ProjectFingerprint,
            LifecycleState: state.LifecycleState,
            BlockingReason: UnityEditorLifecycleSemantics.ResolveBlockingReason(state.LifecycleState),
            CompileState: state.CompileState,
            Generations: state.Generations,
            CanAcceptExecutionRequests: UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(state.LifecycleState),
            ObservedAtUtc: snapshot.ObservedAtUtc,
            ActionRequired: snapshot.ActionRequired,
            PrimaryDiagnostic: null,
            PlayMode: state.PlayMode);
    }

    public static UnityEditorPlayModeSnapshot CreatePlayMode (
        UnityEditorPlayModeState state,
        UnityEditorPlayModeTransition transition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode)
    {
        return new UnityEditorPlayModeSnapshot(
            state,
            transition,
            isPlaying,
            isPlayingOrWillChangePlaymode);
    }
}
