using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary> Represents one projected Play Mode transition for public command output. </summary>
internal sealed record PlayTransitionOutput (
    IpcPlayTransitionCommand Transition,
    IpcPlayTransitionOutcome Result,
    PlayLifecycleSnapshotOutput Before,
    PlayLifecycleSnapshotOutput? After,
    PlayLifecycleSnapshotOutput? Observed,
    IpcApplicationState? ApplicationState);
