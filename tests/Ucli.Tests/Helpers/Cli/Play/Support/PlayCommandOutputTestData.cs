using System.Globalization;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class PlayCommandOutputTestData
{
    public const string ProjectPath = "/repo/UnityProject";

    public const string ServerVersion = "0.5.0";

    public const string UnityVersion = "6000.1.4f1";

    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static IpcCompileState CompileState { get; } = IpcCompileState.Ready;

    public const long CompileGeneration = 12;

    public const long DomainReloadGeneration = 7;

    public static readonly DateTimeOffset ObservedAtUtc =
        DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture);

    public static ProjectIdentityInfo CreateProject ()
    {
        return ProjectIdentityInfoTestFactory.Create(projectPath: ProjectPath);
    }

    public static IpcUnityEditorObservation CreateLifecycleSnapshot (
        IpcEditorLifecycleState lifecycleState,
        IpcPlayModeSnapshot playMode,
        long playModeGeneration)
    {
        var state = new UnityEditorStateSnapshot(
            DaemonEditorMode.Gui,
            lifecycleState,
            CompileState,
            new IpcUnityGenerationSnapshot(
                CompileGeneration,
                DomainReloadGeneration,
                AssetRefreshGeneration: 0,
                PlayModeGeneration: playModeGeneration),
            playMode);
        return new IpcUnityEditorObservation(
            ServerVersion,
            UnityVersion,
            ProjectFingerprint,
            state,
            ObservedAtUtc,
            actionRequired: null,
            primaryDiagnostic: null);
    }

    public static PlayLifecycleSnapshotOutput CreateLifecycleSnapshotOutput (IpcUnityEditorObservation snapshot)
    {
        var state = snapshot.State;
        return new PlayLifecycleSnapshotOutput(
            ServerVersion: snapshot.ServerVersion,
            EditorMode: state.EditorMode,
            UnityVersion: snapshot.UnityVersion,
            ProjectFingerprint: snapshot.ProjectFingerprint,
            LifecycleState: state.LifecycleState,
            BlockingReason: IpcEditorLifecycleSemantics.ResolveBlockingReason(state.LifecycleState),
            CompileState: state.CompileState,
            Generations: state.Generations,
            CanAcceptExecutionRequests: IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(state.LifecycleState),
            ObservedAtUtc: snapshot.ObservedAtUtc,
            ActionRequired: snapshot.ActionRequired,
            PrimaryDiagnostic: null,
            PlayMode: state.PlayMode);
    }

    public static IpcPlayModeSnapshot CreatePlayMode (
        IpcPlayModeState state,
        IpcPlayModeTransition transition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode)
    {
        return new IpcPlayModeSnapshot(
            state,
            transition,
            isPlaying,
            isPlayingOrWillChangePlaymode);
    }
}
