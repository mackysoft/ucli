using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary> Represents one projected Play Mode transition for public command output. </summary>
internal sealed record PlayTransitionOutput (
    PlayLifecycleTransitionCommand Transition,
    PlayLifecycleTransitionOutcome Result,
    PlayLifecycleSnapshotOutput Before,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PlayLifecycleSnapshotOutput? After,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PlayLifecycleSnapshotOutput? Observed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ExecutionApplicationState? ApplicationState);
