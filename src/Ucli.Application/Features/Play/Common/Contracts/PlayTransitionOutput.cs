using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary> Represents one projected Play Mode transition for public command output. </summary>
internal sealed record PlayTransitionOutput (
    IpcPlayTransitionCommand Transition,
    IpcPlayTransitionOutcome Result,
    PlayLifecycleSnapshotOutput Before,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PlayLifecycleSnapshotOutput? After,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PlayLifecycleSnapshotOutput? Observed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcApplicationState? ApplicationState);
