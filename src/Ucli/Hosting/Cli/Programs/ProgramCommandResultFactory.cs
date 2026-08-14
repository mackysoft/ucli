using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Programs.Planning;
using MackySoft.Ucli.Application.Features.Programs.Presets;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Programs;

/// <summary> Projects static Program validation facts to the public CLI result contract. </summary>
internal static class ProgramCommandResultFactory
{
    public static JsonTypeInfo ValidationPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ProgramValidationPayload));

    public static JsonTypeInfo PresetListPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ProgramPresetListPayload));

    public static JsonTypeInfo PresetDescribePayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ProgramPresetDescribePayload));

    public static JsonTypeInfo RunPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ProgramRunStatusPayload));

    public static JsonTypeInfo PlanPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ProgramPlanPayload));

    public static CommandResult CreateValidation (ResolvedUnityProjectContext project, ProgramDefinitionResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        var payload = ProgramValidationPayload.Create(project, result);
        return result.IsSuccess
            ? CommandResult.Success(UcliCommandNames.ProgramValidate, "Program validation completed.", payload)
            : CommandResult.InvalidArgument(UcliCommandNames.ProgramValidate, "Program validation failed.", payload: payload);
    }

    public static CommandResult CreateValidationError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(
            UcliCommandNames.ProgramValidate,
            ApplicationFailure.FromExecutionError(error),
            new ProgramValidationPayload(null, false, null, null, []));
    }

    public static CommandResult CreatePresetList (
        ResolvedUnityProjectContext project,
        ProgramPresetListResult result)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        var payload = ProgramPresetListPayload.Create(project, result);
        return result.IsSuccess
            ? CommandResult.Success(UcliCommandNames.ProgramPresetsList, "Program Presets resolved.", payload)
            : CommandResult.InvalidArgument(UcliCommandNames.ProgramPresetsList, "Program Presets could not be resolved.", payload: payload);
    }

    public static CommandResult CreatePresetListError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(
            UcliCommandNames.ProgramPresetsList,
            ApplicationFailure.FromExecutionError(error),
            new ProgramPresetListPayload(null, [], []));
    }

    public static CommandResult CreatePresetDescribe (
        ResolvedUnityProjectContext project,
        ProgramPresetResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        var payload = ProgramPresetDescribePayload.Create(project, result);
        return result.IsSuccess
            ? CommandResult.Success(UcliCommandNames.ProgramPresetsDescribe, "Program Preset resolved.", payload)
            : CommandResult.InvalidArgument(UcliCommandNames.ProgramPresetsDescribe, "Program Preset could not be resolved.", payload: payload);
    }

    public static CommandResult CreatePresetDescribeError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(
            UcliCommandNames.ProgramPresetsDescribe,
            ApplicationFailure.FromExecutionError(error),
            new ProgramPresetDescribePayload(null, null, null, null, null, null, null, null, []));
    }

    public static CommandResult CreateRunStatus (string command, ProgramRunRecord? run, ProgramSourceManifest? sourceManifest = null, int? commandTimeoutMilliseconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (run is null)
        {
            return CommandResult.InvalidArgument(command, "Program Run was not found.", payload: ProgramRunStatusPayload.NotFound());
        }

        var payload = ProgramRunStatusPayload.Create(run, sourceManifest, commandTimeoutMilliseconds);
        var exitCode = (run.State == ProgramRunState.Completed
            && run.Verdict is Verdict.Fail or Verdict.Incomplete)
            || run.State is ProgramRunState.Failed or ProgramRunState.Cancelled or ProgramRunState.Interrupted
                ? (int)CliExitCode.NonPassingVerdict
                : (int)CliExitCode.Success;
        return new CommandResult(
            IpcProtocol.CurrentVersion,
            command,
            CommandResultStatus.Ok,
            exitCode,
            "Program Run state was read.",
            payload,
            Array.Empty<CommandError>());
    }

    public static CommandResult CreateRunError (string command, ExecutionError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(command, ApplicationFailure.FromExecutionError(error), ProgramRunStatusPayload.NotFound());
    }

    public static CommandResult CreatePlan (ResolvedUnityProjectContext project, ProgramDefinitionResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        var payload = ProgramPlanPayload.Create(project, result);
        return result.IsSuccess
            ? CommandResult.Success(UcliCommandNames.ProgramPlan, "Program plan created.", payload)
            : CommandResult.InvalidArgument(UcliCommandNames.ProgramPlan, "Program plan could not be created.", payload: payload);
    }

    public static CommandResult CreatePlan (
        ResolvedUnityProjectContext project,
        ProgramDefinitionResolutionResult result,
        MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode requestedMode,
        string resolvedMode,
        MackySoft.Ucli.Contracts.Editor.UnityEditorGenerationSnapshot editorGeneration,
        int timeoutMilliseconds,
        bool allowPlayMode,
        bool failFast,
        MackySoft.Ucli.Application.Shared.Configuration.UcliConfig config,
        ProgramPlanPreflightResult preflight)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(editorGeneration);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(preflight);
        var payload = ProgramPlanPayload.Create(project, result, requestedMode, resolvedMode, editorGeneration, timeoutMilliseconds, allowPlayMode, failFast, config, preflight);
        return preflight.IsSuccess
            ? CommandResult.Success(UcliCommandNames.ProgramPlan, "Program plan created.", payload)
            : CommandResult.InvalidArgument(UcliCommandNames.ProgramPlan, "Program plan could not be created.", payload: payload);
    }

    public static CommandResult CreatePlanError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(UcliCommandNames.ProgramPlan, ApplicationFailure.FromExecutionError(error), ProgramPlanPayload.Empty());
    }
}

/// <summary> The closed public payload emitted by <c>program validate</c>. </summary>
internal sealed record ProgramValidationPayload (
    ProgramProjectPayload? Project,
    bool Valid,
    string? DefinitionDigest,
    ProgramSourceManifestPayload? SourceManifest,
    IReadOnlyList<ProgramDiagnosticPayload> Diagnostics)
{
    public static ProgramValidationPayload Create (ResolvedUnityProjectContext project, ProgramDefinitionResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? new ProgramValidationPayload(
                ProgramPayloadProjection.CreateProject(project),
                true,
                result.Definition!.DefinitionDigest.ToString(),
                ProgramSourceManifestPayload.Create(result.Definition.SourceManifest),
                [])
            : new ProgramValidationPayload(
                ProgramPayloadProjection.CreateProject(project),
                false,
                null,
                null,
                result.Diagnostics.Select(ProgramDiagnosticPayload.Create).ToArray());
    }
}

internal sealed record ProgramProjectPayload (string Path, string Fingerprint, string UnityVersion);

internal sealed record ProgramPresetListPayload (
    ProgramProjectPayload? Project,
    IReadOnlyList<ProgramPresetSummaryPayload> Presets,
    IReadOnlyList<ProgramDiagnosticPayload> Diagnostics)
{
    public static ProgramPresetListPayload Create (ResolvedUnityProjectContext project, ProgramPresetListResult result) => new(
        Project: ProgramPayloadProjection.CreateProject(project),
        Presets: result.Presets?.Select(ProgramPresetSummaryPayload.Create).ToArray() ?? [],
        Diagnostics: result.Diagnostics.Select(ProgramDiagnosticPayload.Create).ToArray());
}

internal sealed record ProgramPresetSummaryPayload (
    string Id,
    string Source,
    string Description,
    string DefinitionDigest)
{
    public static ProgramPresetSummaryPayload Create (ProgramPresetResolution preset) => new(
        preset.Id,
        "project",
        preset.Description,
        preset.Definition.DefinitionDigest.ToString());
}

internal sealed record ProgramPresetDescribePayload (
    ProgramProjectPayload? Project,
    string? Id,
    string? Source,
    string? Description,
    string? ReferenceRoot,
    System.Text.Json.JsonElement? Program,
    string? DefinitionDigest,
    ProgramSourceManifestPayload? SourceManifest,
    IReadOnlyList<ProgramDiagnosticPayload> Diagnostics)
{
    public static ProgramPresetDescribePayload Create (ResolvedUnityProjectContext project, ProgramPresetResolutionResult result)
    {
        var preset = result.Preset;
        return new ProgramPresetDescribePayload(
            ProgramPayloadProjection.CreateProject(project),
            preset?.Id,
            preset is null ? null : "project",
            preset?.Description,
            preset?.Definition.SourceManifest.RootPath?.Value,
            preset?.Definition.Program.RootDocument,
            preset?.Definition.DefinitionDigest.ToString(),
            preset is null ? null : ProgramSourceManifestPayload.Create(preset.Definition.SourceManifest),
            result.Diagnostics.Select(ProgramDiagnosticPayload.Create).ToArray());
    }
}

internal sealed record ProgramRunStatusPayload (
    ProgramProjectPayload? Project,
    string? RunId,
    string? DefinitionDigest,
    ProgramAuthorizationPayload? Authorization,
    ProgramConfigurationPayload? Configuration,
    int? RunTimeoutMilliseconds,
    DateTimeOffset? RunDeadlineAtUtc,
    int? CommandTimeoutMilliseconds,
    ProgramRunState? State,
    Verdict? Verdict,
    string? StateRef,
    MackySoft.Ucli.Contracts.ExecutionRef? ExecutionRef,
    ProgramSourceManifestPayload? SourceManifest,
    MackySoft.Ucli.Contracts.ArtifactRef? DefinitionSnapshotRef,
    IReadOnlyList<ProgramRunStepPayload> Steps,
    [property: ItemCount(0, 0)] IReadOnlyList<object> ChildExecutionRefs,
    ProgramSupervisorPayload? Supervisor,
    MackySoft.Ucli.Contracts.Editor.UnityEditorGenerationSnapshot? CurrentEditorGeneration,
    ProgramCancellationPayload? Cancellation,
    ProgramTerminalPayload? Terminal,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? UpdatedAtUtc)
{
    public static ProgramRunStatusPayload NotFound () => new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, [], [], null, null, null, null, null, null);

    public static ProgramRunStatusPayload Create (ProgramRunRecord run, ProgramSourceManifest? sourceManifest, int? commandTimeoutMilliseconds = null) => new(
        ProgramPayloadProjection.CreateProject(run.Project),
        run.RunId.ToString("D"),
        run.DefinitionDigest.ToString(),
        ProgramAuthorizationPayload.Create(run.FixedContext.Authorization),
        ProgramConfigurationPayload.Create(run.FixedContext.Configuration),
        checked((int)(run.DeadlineUtc - run.StartedAtUtc).TotalMilliseconds),
        run.DeadlineUtc,
        commandTimeoutMilliseconds,
        run.State,
        run.Verdict,
        $"program-run/{run.RunId:D}",
        run.CreateExecutionReference(new MackySoft.Ucli.Contracts.ExecutionStatusLocator($"program-run/{run.RunId:D}")),
        sourceManifest is null ? null : ProgramSourceManifestPayload.Create(sourceManifest),
        run.DefinitionSnapshotRef,
        run.Steps.Select(ProgramRunStepPayload.Create).ToArray(),
        [],
        ProgramSupervisorPayload.Create(run),
        run.CurrentEditorGeneration,
        new ProgramCancellationPayload(run.Cancellation.Requested, run.Cancellation.RequestedAtUtc, null, null, run.Cancellation.ReasonCode),
        ProgramTerminalPayload.Create(run),
        run.StartedAtUtc,
        run.UpdatedAtUtc);
}

internal sealed record ProgramAuthorizationPayload (bool AllowDangerous, bool AllowPlayMode, string Digest, DateTimeOffset CapturedAtUtc)
{
    public static ProgramAuthorizationPayload Create (ProgramEffectiveAuthorizationSnapshot snapshot) => new(snapshot.AllowDangerous, snapshot.AllowPlayMode, snapshot.Digest, snapshot.CapturedAtUtc);
}

internal sealed record ProgramConfigurationPayload (
    int SchemaVersion,
    MackySoft.Ucli.Contracts.Configuration.OperationPolicy OperationPolicy,
    MackySoft.Ucli.Contracts.Configuration.PlanTokenMode PlanTokenMode,
    MackySoft.Ucli.Contracts.Configuration.ReadIndexMode ReadIndexDefaultMode,
    IReadOnlyList<string> OperationAllowlist,
    int IpcDefaultTimeoutMilliseconds,
    IReadOnlyDictionary<string, int> IpcTimeoutMillisecondsByCommand,
    string Digest,
    DateTimeOffset CapturedAtUtc)
{
    public static ProgramConfigurationPayload Create (ProgramEffectiveConfigurationSnapshot snapshot) => new(snapshot.SchemaVersion, snapshot.OperationPolicy, snapshot.PlanTokenMode, snapshot.ReadIndexDefaultMode, snapshot.OperationAllowlist, snapshot.IpcDefaultTimeoutMilliseconds, snapshot.IpcTimeoutMillisecondsByCommand, snapshot.Digest.ToString(), snapshot.CapturedAtUtc);
}

internal sealed record ProgramSupervisorPayload (
    string Kind,
    Guid SupervisorId,
    ProgramSupervisorConnection Connection,
    ProgramSupervisorAvailability Availability,
    string RequestedMode,
    string ResolvedMode,
    Guid HostId,
    DateTimeOffset LastObservedAtUtc)
{
    public static ProgramSupervisorPayload Create (ProgramRunRecord run) => new("attachedCli", run.FixedContext.Supervisor.SupervisorId, run.FixedContext.Supervisor.Connection, run.FixedContext.Supervisor.Availability, run.FixedContext.ExecutionMode.RequestedMode, run.FixedContext.ExecutionMode.ResolvedMode, run.FixedContext.Supervisor.HostId, run.FixedContext.Supervisor.LastObservedAtUtc);
}

internal sealed record ProgramRunStepPayload (
    string Command,
    int TimeoutMilliseconds,
    ProgramStepState State,
    Verdict? Verdict,
    DateTimeOffset? PlanningStartedAtUtc,
    DateTimeOffset? StepDeadlineAtUtc,
    MackySoft.Ucli.Contracts.Editor.UnityEditorGenerationSnapshot? GenerationBefore,
    MackySoft.Ucli.Contracts.Editor.UnityEditorGenerationSnapshot? GenerationAfter,
    ExecutionApplicationState ApplicationState,
    MackySoft.Ucli.Contracts.ArtifactRef? RequestPlanRef,
    IReadOnlyList<MackySoft.Ucli.Contracts.ArtifactRef> OperationDescriptorRefs,
    MackySoft.Ucli.Contracts.ExecutionRef? LifecycleExecutionRef,
    UcliNull ChildExecutionRef,
    MackySoft.Ucli.Contracts.ArtifactRef? ResultRef,
    string? ErrorCode,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc)
{
    public static ProgramRunStepPayload Create (ProgramRunStepRecord step) => new(
        step.Command,
        step.TimeoutMilliseconds,
        step.State,
        step.Verdict,
        step.PlanningStartedAtUtc,
        step.DeadlineUtc,
        step.GenerationBefore,
        step.GenerationAfter,
        step.ApplicationState,
        step.RequestPlanRef,
        step.OperationDescriptorRefs,
        step.LifecycleExecutionRef,
        UcliNull.Value,
        step.ResultRef,
        step.ErrorCode,
        step.StartedAtUtc,
        step.CompletedAtUtc);
}

internal sealed record ProgramCancellationPayload (bool Requested, DateTimeOffset? RequestedAtUtc, DateTimeOffset? AcknowledgedAtUtc, string? ActiveChildState, string? ReasonCode);

internal sealed record ProgramTerminalPayload (
    ProgramRunState State,
    Verdict? Verdict,
    string? ReasonCode,
    string Message,
    string? FailedInstancePath,
    ExecutionApplicationState ApplicationState,
    int CompletedStepCount,
    int UnstartedStepCount,
    DateTimeOffset CompletedAtUtc,
    MackySoft.Ucli.Contracts.ArtifactRef RecordRef)
{
    public static ProgramTerminalPayload? Create (ProgramRunRecord run)
    {
        if (!ProgramRunStateSemantics.IsTerminal(run.State) || run.TerminalRecordRef is null)
        {
            return null;
        }
        var failedIndex = run.Steps
            .Select((step, index) => (step, index))
            .FirstOrDefault(static item => item.step.State is ProgramStepState.Failed or ProgramStepState.Cancelled or ProgramStepState.Interrupted)
            .index;
        return new ProgramTerminalPayload(
            run.State,
            run.Verdict,
            run.TerminalReasonCode,
            run.TerminalReasonCode ?? "Program Run completed.",
            run.Steps.Any(static step => step.State is ProgramStepState.Failed or ProgramStepState.Cancelled or ProgramStepState.Interrupted)
                ? $"/steps/{failedIndex}"
                : null,
            run.ApplicationState,
            run.Steps.Count(static step => step.State == ProgramStepState.Completed),
            run.Steps.Count(static step => step.StartedAtUtc is null),
            run.UpdatedAtUtc,
            run.TerminalRecordRef);
    }
}

internal sealed record ProgramPlanPayload (
    ProgramProjectPayload? Project,
    string? DefinitionDigest,
    ProgramSourceManifestPayload? SourceManifest,
    string? RequestedMode,
    string? ResolvedMode,
    MackySoft.Ucli.Contracts.Editor.UnityEditorGenerationSnapshot? EditorGeneration,
    int? TimeoutMilliseconds,
    ProgramPlanningOptionsPayload? PlanningOptions,
    IReadOnlyList<ProgramPlanStepPayload> Steps,
    IReadOnlyList<string> RequiredRunOptions,
    IReadOnlyList<ProgramDiagnosticPayload> Diagnostics)
{
    public static ProgramPlanPayload Empty () => new(null, null, null, null, null, null, null, null, [], [], []);

    public static ProgramPlanPayload Create (ResolvedUnityProjectContext project, ProgramDefinitionResolutionResult result)
    {
        if (!result.IsSuccess)
        {
            return new ProgramPlanPayload(ProgramPayloadProjection.CreateProject(project), null, null, null, null, null, null, null, [], [], result.Diagnostics.Select(ProgramDiagnosticPayload.Create).ToArray());
        }

        var definition = result.Definition!;
        var projection = ProgramPlanProjection.Create(definition.Program, 0);
        return new ProgramPlanPayload(
            ProgramPayloadProjection.CreateProject(project),
            definition.DefinitionDigest.ToString(),
            ProgramSourceManifestPayload.Create(definition.SourceManifest),
            null,
            null,
            null,
            null,
            null,
            projection.Steps.Select(ProgramPlanStepPayload.Create).ToArray(),
            [],
            []);
    }

    public static ProgramPlanPayload Create (
        ResolvedUnityProjectContext project,
        ProgramDefinitionResolutionResult result,
        MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode requestedMode,
        string resolvedMode,
        MackySoft.Ucli.Contracts.Editor.UnityEditorGenerationSnapshot editorGeneration,
        int timeoutMilliseconds,
        bool allowPlayMode,
        bool failFast,
        MackySoft.Ucli.Application.Shared.Configuration.UcliConfig config,
        ProgramPlanPreflightResult preflight)
    {
        var definition = result.Definition ?? throw new ArgumentException("Program plan requires a resolved definition.", nameof(result));
        var projection = ProgramPlanProjection.Create(definition.Program, 0);
        var steps = projection.Steps.Select(step => ProgramPlanStepPayload.Create(
            step,
            definition.Program.Steps[step.Index],
            config,
            preflight.RequiredRunOptions.TryGetValue(step.Index, out var options) ? options : [])).ToArray();
        return new ProgramPlanPayload(
            ProgramPayloadProjection.CreateProject(project),
            definition.DefinitionDigest.ToString(),
            ProgramSourceManifestPayload.Create(definition.SourceManifest),
            requestedMode switch
            {
                MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode.Auto => "auto",
                MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode.Daemon => "daemon",
                MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode.Oneshot => "oneshot",
                _ => throw new ArgumentOutOfRangeException(nameof(requestedMode)),
            },
            resolvedMode,
            editorGeneration,
            timeoutMilliseconds,
            new ProgramPlanningOptionsPayload(allowPlayMode, failFast),
            steps,
            steps.SelectMany(static step => step.RequiredRunOptions).Distinct(StringComparer.Ordinal).OrderBy(static option => option, StringComparer.Ordinal).ToArray(),
            preflight.Diagnostic is null ? [] : [ProgramDiagnosticPayload.Create(preflight.Diagnostic)]);
    }
}

internal sealed record ProgramPlanningOptionsPayload (bool AllowPlayMode, bool FailFast);

internal sealed record ProgramPlanStepPayload (
    string Command,
    int TimeoutMilliseconds,
    string PlanningState,
    IReadOnlyList<string> RequiredRunOptions,
    object? ChildExecutionKind,
    string GenerationBehavior)
{
    public static ProgramPlanStepPayload Create (ProgramPlanStepProjection step) => new(
        step.Command,
        1,
        step.State == ProgramPlanStepState.Current ? "ready" : "deferred",
        [],
        null,
        step.Command is "refresh" or "compile" or "play.enter" or "play.exit" ? "segmentBoundary" : "sameEditorGeneration");

    public static ProgramPlanStepPayload Create (
        ProgramPlanStepProjection step,
        ProgramStep source,
        MackySoft.Ucli.Application.Shared.Configuration.UcliConfig config,
        IReadOnlyList<string> requiredRunOptions) => new(
        step.Command,
        source.TimeoutMilliseconds ?? checked((int)MackySoft.Ucli.Application.Shared.Execution.Timeout.IpcCommandTimeoutResolver.ResolveNormalized(null, new UcliCommand(step.Command), config).Timeout!.Value.TotalMilliseconds),
        step.State == ProgramPlanStepState.Current ? "ready" : "deferred",
        requiredRunOptions,
        null,
        source is RefreshProgramStep or CompileProgramStep or PlayEnterProgramStep or PlayExitProgramStep ? "segmentBoundary" : "sameEditorGeneration");

}

internal sealed record ProgramSourceManifestPayload (
    string Digest,
    string RootSource,
    string? RootPath,
    string? PresetId,
    string ProgramDigest,
    IReadOnlyList<ProgramSourceManifestEntryPayload> Sources)
{
    public static ProgramSourceManifestPayload Create (ProgramSourceManifest manifest) => new(
        manifest.Digest.ToString(),
        manifest.RootSource.ToString().ToLowerInvariant(),
        manifest.RootPath?.Value,
        manifest.PresetId,
        manifest.ProgramDigest.ToString(),
        manifest.Sources.Select(ProgramSourceManifestEntryPayload.Create).ToArray());
}

internal sealed record ProgramSourceManifestEntryPayload (
    string InstancePath,
    string Role,
    string Path,
    string DocumentDigest,
    int ByteLength)
{
    public static ProgramSourceManifestEntryPayload Create (ProgramSourceManifestEntry entry) => new(
        entry.InstancePath,
        entry.Role,
        entry.Path.Value,
        entry.DocumentDigest.ToString(),
        entry.ByteLength);
}

internal sealed record ProgramDiagnosticPayload (
    string Code,
    string Severity,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Document,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? InstancePath)
{
    public static ProgramDiagnosticPayload Create (ProgramDiagnostic diagnostic) => new(
        diagnostic.Code,
        "error",
        diagnostic.Message,
        null,
        diagnostic.InstancePath);
}

internal static class ProgramPayloadProjection
{
    public static ProgramProjectPayload CreateProject (ResolvedUnityProjectContext project) => new(
        project.UnityProjectRoot.Value,
        project.ProjectFingerprint.ToString(),
        project.UnityVersion);

    public static ProgramProjectPayload CreateProject (MackySoft.Ucli.Contracts.Projects.UnityProjectIdentity project) => new(
        project.ProjectPath,
        project.ProjectFingerprint.ToString(),
        project.UnityVersion);
}
