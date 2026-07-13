using System.Globalization;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Shared.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class PlayCommandOutputTestData
{
    public const string ProjectPath = "/repo/UnityProject";

    public const string ServerVersion = "0.5.0";

    public const string UnityVersion = "6000.1.4f1";

    public static readonly ProjectFingerprint ProjectFingerprint = ProjectFingerprintTestFactory.Create("project-fingerprint");

    public const string CompileState = "ready";

    public const string CompileGeneration = "12";

    public const string DomainReloadGeneration = "7";

    public static readonly DateTimeOffset ObservedAtUtc =
        DateTimeOffset.Parse("2026-05-21T00:00:00+00:00", CultureInfo.InvariantCulture);

    public static ProjectIdentityInfo CreateProject ()
    {
        return ProjectIdentityInfoTestFactory.Create(projectPath: ProjectPath);
    }

    public static IpcPlayLifecycleSnapshot CreateLifecycleSnapshot (
        string lifecycleState,
        string? blockingReason,
        bool canAcceptExecutionRequests,
        IpcPlayModeSnapshot playMode)
    {
        return new IpcPlayLifecycleSnapshot(
            ServerVersion: ServerVersion,
            EditorMode: "gui",
            UnityVersion: UnityVersion,
            ProjectFingerprint: ProjectFingerprint,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: CompileState,
            CompileGeneration: CompileGeneration,
            DomainReloadGeneration: DomainReloadGeneration,
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: ObservedAtUtc,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: playMode);
    }

    public static PlayLifecycleSnapshotOutput CreateLifecycleSnapshotOutput (IpcPlayLifecycleSnapshot snapshot)
    {
        return new PlayLifecycleSnapshotOutput(
            ServerVersion: snapshot.ServerVersion,
            EditorMode: snapshot.EditorMode,
            UnityVersion: snapshot.UnityVersion,
            ProjectFingerprint: snapshot.ProjectFingerprint,
            LifecycleState: snapshot.LifecycleState,
            BlockingReason: snapshot.BlockingReason,
            CompileState: snapshot.CompileState,
            CompileGeneration: snapshot.CompileGeneration,
            DomainReloadGeneration: snapshot.DomainReloadGeneration,
            CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests,
            ObservedAtUtc: snapshot.ObservedAtUtc,
            ActionRequired: snapshot.ActionRequired,
            PrimaryDiagnostic: null,
            PlayMode: CreatePlayModeOutput(snapshot.PlayMode!));
    }

    public static IpcPlayModeSnapshot CreatePlayMode (
        string state,
        string transition,
        bool isPlaying,
        bool isPlayingOrWillChangePlaymode,
        string generation)
    {
        return new IpcPlayModeSnapshot(
            State: state,
            Transition: transition,
            IsPlaying: isPlaying,
            IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode,
            Generation: generation);
    }

    public static PlayModeSnapshotOutput CreatePlayModeOutput (IpcPlayModeSnapshot snapshot)
    {
        return new PlayModeSnapshotOutput(
            State: snapshot.State,
            Transition: snapshot.Transition,
            IsPlaying: snapshot.IsPlaying,
            IsPlayingOrWillChangePlaymode: snapshot.IsPlayingOrWillChangePlaymode,
            Generation: snapshot.Generation);
    }
}
