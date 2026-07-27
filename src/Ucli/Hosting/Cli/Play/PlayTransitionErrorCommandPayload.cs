using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Represents the last available Play Mode transition observation for a failed command. </summary>
internal sealed record PlayTransitionErrorCommandPayload (
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProjectIdentityInfo? Project,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonStatusKind? DaemonStatus,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ServerVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonEditorMode? EditorMode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcEditorLifecycleState? LifecycleState,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcEditorBlockingReason? BlockingReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcCompileState? CompileState,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcUnityGenerationSnapshot? Generations,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? CanAcceptExecutionRequests,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? ObservedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonDiagnosisActionRequired? ActionRequired,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcPlayModeSnapshot? PlayMode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PlayTransitionOutput? Transition,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? TimeoutMilliseconds)
    : CommandErrorPayload<PlayTransitionErrorCommandPayload>
{
    public static PlayTransitionErrorCommandPayload From (PlayEnterExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new PlayTransitionErrorCommandPayload(
            output.Project,
            output.DaemonStatus,
            output.ServerVersion,
            output.EditorMode,
            output.LifecycleState,
            output.BlockingReason,
            output.CompileState,
            output.Generations,
            output.CanAcceptExecutionRequests,
            output.ObservedAtUtc,
            output.ActionRequired,
            output.PrimaryDiagnostic,
            output.PlayMode,
            output.Transition,
            output.TimeoutMilliseconds);
    }

    public static PlayTransitionErrorCommandPayload From (PlayExitExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new PlayTransitionErrorCommandPayload(
            output.Project,
            output.DaemonStatus,
            output.ServerVersion,
            output.EditorMode,
            output.LifecycleState,
            output.BlockingReason,
            output.CompileState,
            output.Generations,
            output.CanAcceptExecutionRequests,
            output.ObservedAtUtc,
            output.ActionRequired,
            output.PrimaryDiagnostic,
            output.PlayMode,
            output.Transition,
            output.TimeoutMilliseconds);
    }
}
