using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Programs.Planning;
using MackySoft.Ucli.Application.Features.Programs.Presets;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Features.Programs.Supervision;
using MackySoft.Ucli.Application.Features.Programs.Validate;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Execution;
using Microsoft.Extensions.DependencyInjection;
using ExecutionMode = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;
using ExecutionTarget = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionTarget;

namespace MackySoft.Ucli.Hosting.Cli.Programs;

internal sealed class ProgramPlanCommand
{
    private readonly IProgramValidationService validationService;
    private readonly ProgramInputResolver inputResolver;
    private readonly IProgramRunHostContextResolver hostContextResolver;
    private readonly ProgramPlanPreflightService planPreflightService;
    private readonly TimeProvider timeProvider;
    private readonly ICommandResultWriter commandResultWriter;

    public ProgramPlanCommand (
        IProgramValidationService validationService,
        ProgramInputResolver inputResolver,
        IProgramRunHostContextResolver hostContextResolver,
        ProgramPlanPreflightService planPreflightService,
        TimeProvider timeProvider,
        ICommandResultWriter commandResultWriter)
    {
        this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        this.inputResolver = inputResolver ?? throw new ArgumentNullException(nameof(inputResolver));
        this.hostContextResolver = hostContextResolver ?? throw new ArgumentNullException(nameof(hostContextResolver));
        this.planPreflightService = planPreflightService ?? throw new ArgumentNullException(nameof(planPreflightService));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Plans the current Program execution frontier. </summary>
    /// <param name="programPath">--programPath, Optional root Program JSON file path.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="failFast">--failFast, Fails immediately when a Program Step cannot enter a waitable Unity lifecycle state.</param>
    /// <param name="allowPlayMode">--allowPlayMode, Allows planning Play Mode operations.</param>
    [Command(UcliCommandNames.Plan)]
    public async Task<int> PlanAsync (
        string? preset = null,
        [AbsolutePathArgumentParser] AbsolutePath? programPath = null,
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        string? mode = null,
        string? timeout = null,
        bool failFast = false,
        bool allowPlayMode = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var modeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!modeResult.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreatePlanError(modeResult.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreatePlanError(timeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var input = await inputResolver.ResolveAsync(preset, programPath, projectPath, cancellationToken).ConfigureAwait(false);
        if (input.Error is not null)
        {
            var error = ProgramCommandResultFactory.CreatePlanError(input.Error);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var resolution = input.HasValidationFailure
            ? ProgramDefinitionResolutionResult.Failure(input.Diagnostics)
            : input.ResolvedDefinition ?? await validationService.ValidateAsync(input.Input!, cancellationToken).ConfigureAwait(false);
        if (!resolution.IsSuccess)
        {
            var failed = ProgramCommandResultFactory.CreatePlan(input.Project!.UnityProject, resolution);
            commandResultWriter.WriteToStandardOutput(failed);
            return failed.ExitCode;
        }
        var resolvedTimeout = IpcCommandTimeoutResolver.ResolveNormalized(timeoutResult.TimeoutMilliseconds, UcliCommandIds.ProgramPlan, input.Project!.Config);
        if (!resolvedTimeout.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreatePlanError(resolvedTimeout.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var deadline = ExecutionDeadline.Start(resolvedTimeout.Timeout!.Value, timeProvider);
        var authorization = new IpcProgramEffectiveAuthorizationSnapshot(
            allowDangerous: false,
            allowPlayMode,
            IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(false, allowPlayMode));
        var host = await hostContextResolver.ResolveAsync(
                input.Project,
                modeResult.Mode ?? ExecutionMode.Auto,
                deadline,
                authorization,
                ProgramGuiRequirement.Find(resolution.Definition!.Program),
                cancellationToken)
            .ConfigureAwait(false);
        if (!host.IsSuccess)
        {
            var error = CommandFailureProjector.Create(UcliCommandNames.ProgramPlan, host.Failure!, ProgramPlanPayload.Empty());
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        await using var hostContext = host.Context!;
        var preflight = await planPreflightService.ValidateAsync(
                resolution.Definition!,
                input.Project,
                hostContext.Binding,
                deadline,
                allowPlayMode,
                failFast,
                cancellationToken)
            .ConfigureAwait(false);
        var result = ProgramCommandResultFactory.CreatePlan(input.Project.UnityProject, resolution, modeResult.Mode ?? ExecutionMode.Auto,
            hostContext.Binding.Target == ExecutionTarget.Daemon ? "daemon" : "oneshot", hostContext.Generation,
            checked((int)resolvedTimeout.Timeout.Value.TotalMilliseconds), allowPlayMode, failFast, input.Project.Config, preflight);
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }
}

internal sealed class ProgramRunCommand
{
    private readonly IProgramValidationService validationService;
    private readonly ProgramInputResolver inputResolver;
    private readonly IProjectContextResolver projectContextResolver;
    private readonly IProgramRunHostContextResolver hostContextResolver;
    private readonly IServiceProvider serviceProvider;
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly IProcessIdentityObserver processIdentityObserver;
    private readonly IGuidGenerator guidGenerator;
    private readonly TimeProvider timeProvider;
    private readonly CliStreamEntryWriterFactory streamEntryWriterFactory;
    private readonly ICommandResultWriter commandResultWriter;

    public ProgramRunCommand (
        IProgramValidationService validationService,
        ProgramInputResolver inputResolver,
        IProjectContextResolver projectContextResolver,
        IProgramRunHostContextResolver hostContextResolver,
        IServiceProvider serviceProvider,
        IProgramRunStoreFactory storeFactory,
        IProcessIdentityObserver processIdentityObserver,
        IGuidGenerator guidGenerator,
        TimeProvider timeProvider,
        CliStreamEntryWriterFactory streamEntryWriterFactory,
        ICommandResultWriter commandResultWriter)
    {
        this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        this.inputResolver = inputResolver ?? throw new ArgumentNullException(nameof(inputResolver));
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.hostContextResolver = hostContextResolver ?? throw new ArgumentNullException(nameof(hostContextResolver));
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.processIdentityObserver = processIdentityObserver ?? throw new ArgumentNullException(nameof(processIdentityObserver));
        this.guidGenerator = guidGenerator ?? throw new ArgumentNullException(nameof(guidGenerator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.streamEntryWriterFactory = streamEntryWriterFactory ?? throw new ArgumentNullException(nameof(streamEntryWriterFactory));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Starts and supervises one Program Run. </summary>
    /// <param name="programPath">--programPath, Optional root Program JSON file path.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="failFast">--failFast, Fails immediately when a Program Step cannot enter a waitable Unity lifecycle state.</param>
    /// <param name="allowDangerous">--allowDangerous, Allows explicitly authorized dangerous operations.</param>
    /// <param name="allowPlayMode">--allowPlayMode, Allows explicitly authorized Play Mode operations.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    [Command(UcliCommandNames.RunSubcommand)]
    public async Task<int> RunAsync (
        string? preset = null,
        [AbsolutePathArgumentParser] AbsolutePath? programPath = null,
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        string? mode = null,
        string? timeout = null,
        bool failFast = false,
        bool allowDangerous = false,
        bool allowPlayMode = false,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var formatResult = CliStreamEntryFormatOptionNormalizer.Normalize(format);
        if (!formatResult.IsSuccess)
        {
            return Write(ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramRun, formatResult.Error!));
        }
        var modeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!modeResult.IsSuccess)
        {
            return Write(ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramRun, modeResult.Error!));
        }
        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            return Write(ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramRun, timeoutResult.Error!));
        }
        var input = await inputResolver.ResolveAsync(preset, programPath, projectPath, cancellationToken).ConfigureAwait(false);
        if (input.Error is not null)
        {
            return Write(ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramRun, input.Error));
        }
        var definition = input.HasValidationFailure
            ? ProgramDefinitionResolutionResult.Failure(input.Diagnostics)
            : input.ResolvedDefinition ?? await validationService.ValidateAsync(input.Input!, cancellationToken).ConfigureAwait(false);
        if (!definition.IsSuccess)
        {
            return Write(ProgramCommandResultFactory.CreateValidation(input.Project!.UnityProject, definition));
        }
        var context = input.Project!;
        var resolvedTimeout = IpcCommandTimeoutResolver.ResolveNormalized(timeoutResult.TimeoutMilliseconds, UcliCommandIds.ProgramRun, context.Config);
        if (!resolvedTimeout.IsSuccess)
        {
            return Write(ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramRun, resolvedTimeout.Error!));
        }
        var deadline = ExecutionDeadline.Start(resolvedTimeout.Timeout!.Value, timeProvider);
        var authorization = CreateAuthorization(allowDangerous, allowPlayMode);
        var host = await hostContextResolver.ResolveAsync(
                context,
                modeResult.Mode ?? ExecutionMode.Auto,
                deadline,
                authorization,
                ProgramGuiRequirement.Find(definition.Definition!.Program),
                cancellationToken)
            .ConfigureAwait(false);
        if (!host.IsSuccess)
        {
            return Write(CommandFailureProjector.Create(UcliCommandNames.ProgramRun, host.Failure!, ProgramRunStatusPayload.NotFound()));
        }

        await using var hostContext = host.Context!;
        // The persisted owner identity and the attached Supervisor must be the
        // same process identity.  A second observation of the process would
        // cause the owner fence to reject the freshly registered Run.
        var owner = ProcessLivenessProbe.CaptureCurrentProcess();
        var fixedContext = CreateFixedContext(context, hostContext, modeResult.Mode ?? ExecutionMode.Auto, authorization, failFast, owner);
        var port = new ProgramLifecycleStepExecutionPort(
            hostContext,
            storeFactory,
            serviceProvider.GetRequiredService<IReadyService>(),
            serviceProvider.GetRequiredService<IScreenshotCaptureService>(),
            serviceProvider.GetRequiredService<IRefreshService>(),
            serviceProvider.GetRequiredService<ICompileService>(),
            serviceProvider.GetRequiredService<IPlayEnterService>(),
            serviceProvider.GetRequiredService<IPlayExitService>(),
            serviceProvider.GetRequiredService<ProgramCallPreflightService>(),
            serviceProvider.GetRequiredService<IProgramArtifactStoreFactory>(),
            serviceProvider.GetRequiredService<ILifecycleExecutionReconnectResolver>(),
            timeProvider);
        var useCases = ProgramInternalUseCaseComposition.Create(
            storeFactory,
            serviceProvider.GetRequiredService<ProgramRunPersistenceService>(),
            port,
            new ProgramRunStartNotificationPort(new CliCommandProgressSink(
                formatResult.Format,
                streamEntryWriterFactory.Create(UcliCommandNames.ProgramRun),
                ProgramRunProgressTextProjector.Instance)),
            processIdentityObserver,
            guidGenerator,
            timeProvider,
            owner);
        var pendingSteps = definition.Definition!.Program.Steps.Select(step => new ProgramRunPendingStep(
            ProgramStepCommand.Project(step), ResolveStepTimeout(step, context.Config))).ToArray();
        var registration = new ProgramRunRegistrationRequest(
            context.UnityProject,
            new UnityProjectIdentity(context.UnityProject.UnityProjectRoot.Value, context.UnityProject.ProjectFingerprint, context.UnityProject.UnityVersion),
            definition.Definition,
            fixedContext,
            hostContext.Host,
            hostContext.Generation,
            hostContext.Generation,
            deadline.UtcDeadline,
            pendingSteps);
        var run = await useCases.RunStart.StartAsync(registration, deadline, cancellationToken).ConfigureAwait(false);
        run = await WaitForTerminalAsync(useCases.Supervisor, context.UnityProject, run, deadline, cancellationToken).ConfigureAwait(false);
        return Write(ProgramCommandResultFactory.CreateRunStatus(
            UcliCommandNames.ProgramRun,
            run,
            definition.Definition!.SourceManifest,
            checked((int)resolvedTimeout.Timeout.Value.TotalMilliseconds)));
    }

    private static async ValueTask<ProgramRunRecord> WaitForTerminalAsync (
        ProgramAttachedSupervisor supervisor,
        ResolvedUnityProjectContext project,
        ProgramRunRecord current,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        while (!ProgramRunStateSemantics.IsTerminal(current.State))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recovered = await supervisor.RecoverAsync(project, current.RunId, deadline, cancellationToken).ConfigureAwait(false);
            if (recovered is null)
            {
                throw new InvalidOperationException("Program Run disappeared while its attached Supervisor was waiting for completion.");
            }
            current = recovered;
            if (ProgramRunStateSemantics.IsTerminal(current.State))
            {
                break;
            }
            if (!deadline.TryGetRemainingTimeout(out var remaining))
            {
                // RecoverAsync observes the same deadline and selects the Run's
                // terminal transition. A subsequent iteration reads that fixed result.
                continue;
            }
            await Task.Delay(remaining < TimeSpan.FromMilliseconds(25) ? remaining : TimeSpan.FromMilliseconds(25), deadline.Clock, cancellationToken).ConfigureAwait(false);
        }
        return current;
    }

    private int Write (CommandResult result)
    {
        commandResultWriter.WriteToStandardOutput(result);
        return result.ExitCode;
    }

    private ProgramRunFixedContext CreateFixedContext (
        ProjectContext project,
        ProgramRunHostContext host,
        ExecutionMode requestedMode,
        IpcProgramEffectiveAuthorizationSnapshot effectiveAuthorization,
        bool failFast,
        ProcessIdentity owner)
    {
        var now = timeProvider.GetUtcNow();
        var configuration = CreateEffectiveConfiguration(project, now);
        var authorization = new ProgramEffectiveAuthorizationSnapshot(
            effectiveAuthorization.AllowDangerous,
            effectiveAuthorization.AllowPlayMode,
            effectiveAuthorization.Digest.ToString(),
            now);
        return new ProgramRunFixedContext(authorization, configuration,
            new ProgramExecutionModeSnapshot(GetModeText(requestedMode), host.Binding.Target == ExecutionTarget.Daemon ? "daemon" : "oneshot"),
            new ProgramAttachedSupervisorSnapshot(Guid.NewGuid(), host.Host.EditorInstanceId, owner,
                ProgramSupervisorConnection.Connected, ProgramSupervisorAvailability.Available, now))
        {
            FailFast = failFast,
        };
    }

    internal static ProgramEffectiveConfigurationSnapshot CreateEffectiveConfiguration (
        ProjectContext project,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        var effectiveTimeouts = IpcTimeoutDefaults.SupportedCommands.ToDictionary(
            command => command.Name,
            command => IpcCommandTimeoutResolver.ResolveNormalized(null, command, project.Config).Timeout!.Value.TotalMilliseconds is var value
                ? checked((int)value)
                : throw new InvalidOperationException(),
            StringComparer.Ordinal);
        return new ProgramEffectiveConfigurationSnapshot(
            project.Config.SchemaVersion,
            project.Config.OperationPolicy,
            project.Config.PlanTokenMode,
            project.Config.ReadIndexDefaultMode,
            project.Config.OperationAllowlist.ToArray(),
            project.Config.IpcDefaultTimeoutMilliseconds,
            effectiveTimeouts,
            IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
                project.Config.SchemaVersion,
                TextVocabulary.GetText(project.Config.OperationPolicy),
                TextVocabulary.GetText(project.Config.PlanTokenMode),
                TextVocabulary.GetText(project.Config.ReadIndexDefaultMode),
                project.Config.OperationAllowlist,
                project.Config.IpcDefaultTimeoutMilliseconds,
                effectiveTimeouts),
            capturedAtUtc);
    }

    private static IpcProgramEffectiveAuthorizationSnapshot CreateAuthorization (bool allowDangerous, bool allowPlayMode) =>
        new(allowDangerous, allowPlayMode, IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(allowDangerous, allowPlayMode));

    private static int ResolveStepTimeout (ProgramStep step, UcliConfig config)
    {
        var command = new UcliCommand(ProgramStepCommand.Project(step));
        return step.TimeoutMilliseconds ?? checked((int)IpcCommandTimeoutResolver.ResolveNormalized(null, command, config).Timeout!.Value.TotalMilliseconds);
    }

    private static string GetModeText (ExecutionMode mode) => mode switch
    {
        ExecutionMode.Auto => "auto",
        ExecutionMode.Daemon => "daemon",
        ExecutionMode.Oneshot => "oneshot",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}

internal static class ProgramStepCommand
{
    public static string Project (ProgramStep step) => step switch
    {
        CallProgramStep => "call",
        ReadyProgramStep => "ready",
        RefreshProgramStep => "refresh",
        CompileProgramStep => "compile",
        PlayEnterProgramStep => "play.enter",
        PlayExitProgramStep => "play.exit",
        ScreenshotGameProgramStep => "screenshot.game",
        ScreenshotSceneProgramStep => "screenshot.scene",
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };
}

/// <summary> Emits the Run creation entry only after the Run is durably readable. </summary>
internal sealed class ProgramRunStartNotificationPort : IProgramRunStartNotificationPort
{
    private readonly MackySoft.Ucli.Application.Shared.Execution.Progress.ICommandProgressSink progressSink;

    public ProgramRunStartNotificationPort (MackySoft.Ucli.Application.Shared.Execution.Progress.ICommandProgressSink progressSink)
    {
        this.progressSink = progressSink ?? throw new ArgumentNullException(nameof(progressSink));
    }

    public ValueTask NotifyAsync (ProgramRunRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        return progressSink.OnEntryAsync("program.run.started", new ProgramRunStartedProgressEntry(
            run.RunId,
            run.DefinitionDigest.ToString(),
            run.State,
            run.StartedAtUtc), cancellationToken);
    }
}

internal sealed record ProgramRunStartedProgressEntry (
    Guid RunId,
    string DefinitionDigest,
    ProgramRunState State,
    DateTimeOffset StartedAtUtc);

internal sealed class ProgramRunProgressTextProjector : ICliCommandProgressTextProjector
{
    public static ProgramRunProgressTextProjector Instance { get; } = new();

    private ProgramRunProgressTextProjector () { }

    public bool TryCreateTextEntry<TPayload> (string eventName, TPayload payload, out string text)
        where TPayload : notnull
    {
        text = payload is ProgramRunStartedProgressEntry started
            ? $"program runId={started.RunId:D} started"
            : CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
        return true;
    }
}

internal sealed class ProgramStatusCommand
{
    private readonly IProjectContextResolver projectContextResolver;
    private readonly ProgramRunStatusCancelReconciliationService statusCancelService;
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly ICommandResultWriter commandResultWriter;

    public ProgramStatusCommand (
        IProjectContextResolver projectContextResolver,
        ProgramRunStatusCancelReconciliationService statusCancelService,
        IProgramRunStoreFactory storeFactory,
        ICommandResultWriter commandResultWriter)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.statusCancelService = statusCancelService ?? throw new ArgumentNullException(nameof(statusCancelService));
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Reads one persisted Program Run without advancing it. </summary>
    /// <param name="runId">--runId, Program Run identifier.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    [Command(UcliCommandNames.Status)]
    public async Task<int> StatusAsync (
        Guid runId,
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var context = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!context.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramStatus, context.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramStatus, timeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var resolvedTimeout = IpcCommandTimeoutResolver.ResolveNormalized(timeoutResult.TimeoutMilliseconds, UcliCommandIds.ProgramStatus, context.Context!.Config);
        if (!resolvedTimeout.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramStatus, resolvedTimeout.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        using var deadlineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCancellation.CancelAfter(resolvedTimeout.Timeout!.Value);
        try
        {
            var run = await statusCancelService.GetStatusAsync(context.Context.UnityProject, runId, deadlineCancellation.Token).ConfigureAwait(false);
            var definition = run is null ? null : await storeFactory.ForProject(context.Context.UnityProject).ReadDefinitionAsync(run.RunId, deadlineCancellation.Token).ConfigureAwait(false);
            var result = ProgramCommandResultFactory.CreateRunStatus(UcliCommandNames.ProgramStatus, run, definition?.Definition.SourceManifest,
                checked((int)resolvedTimeout.Timeout.Value.TotalMilliseconds));
            commandResultWriter.WriteToStandardOutput(result);
            return result.ExitCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var error = ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramStatus,
                ExecutionError.Timeout("Program status deadline elapsed before reconciliation completed.", new UcliCode("PROGRAM_STATUS_TIMEOUT")));
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
    }
}

internal sealed class ProgramCancelCommand
{
    private readonly IProjectContextResolver projectContextResolver;
    private readonly ProgramRunStatusCancelReconciliationService statusCancelService;
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly ICommandResultWriter commandResultWriter;

    public ProgramCancelCommand (
        IProjectContextResolver projectContextResolver,
        ProgramRunStatusCancelReconciliationService statusCancelService,
        IProgramRunStoreFactory storeFactory,
        ICommandResultWriter commandResultWriter)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.statusCancelService = statusCancelService ?? throw new ArgumentNullException(nameof(statusCancelService));
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Requests cancellation of one persisted Program Run. </summary>
    /// <param name="runId">--runId, Program Run identifier.</param>
    /// <param name="reasonCode">--reasonCode, Optional cancellation reason.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    [Command(UcliCommandNames.CancelSubcommand)]
    public async Task<int> CancelAsync (
        Guid runId,
        string? reasonCode = null,
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        string? timeout = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var context = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!context.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramCancel, context.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var timeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!timeoutResult.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramCancel, timeoutResult.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        var resolvedTimeout = IpcCommandTimeoutResolver.ResolveNormalized(timeoutResult.TimeoutMilliseconds, UcliCommandIds.ProgramCancel, context.Context!.Config);
        if (!resolvedTimeout.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramCancel, resolvedTimeout.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
        using var deadlineCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCancellation.CancelAfter(resolvedTimeout.Timeout!.Value);
        try
        {
            var run = await statusCancelService.CancelAsync(context.Context.UnityProject, runId, reasonCode, deadlineCancellation.Token).ConfigureAwait(false);
            var definition = run is null ? null : await storeFactory.ForProject(context.Context.UnityProject).ReadDefinitionAsync(run.RunId, deadlineCancellation.Token).ConfigureAwait(false);
            var result = ProgramCommandResultFactory.CreateRunStatus(UcliCommandNames.ProgramCancel, run, definition?.Definition.SourceManifest,
                checked((int)resolvedTimeout.Timeout.Value.TotalMilliseconds));
            commandResultWriter.WriteToStandardOutput(result);
            return result.ExitCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var error = ProgramCommandResultFactory.CreateRunError(UcliCommandNames.ProgramCancel,
                ExecutionError.Timeout("Program cancellation deadline elapsed before reconciliation completed.", new UcliCode("PROGRAM_CANCEL_TIMEOUT")));
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }
    }
}

internal sealed class ProgramPresetsListCommand
{
    private readonly IProjectContextResolver projectContextResolver;
    private readonly IUcliConfigStore configStore;
    private readonly IProgramPresetCatalog presetCatalog;
    private readonly ICommandResultWriter commandResultWriter;

    public ProgramPresetsListCommand (
        IProjectContextResolver projectContextResolver,
        IUcliConfigStore configStore,
        IProgramPresetCatalog presetCatalog,
        ICommandResultWriter commandResultWriter)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        this.presetCatalog = presetCatalog ?? throw new ArgumentNullException(nameof(presetCatalog));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Lists available Program Presets. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    [Command(UcliCommandNames.ListSubcommand)]
    public async Task<int> ListAsync (
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var context = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!context.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreatePresetListError(context.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }

        var resolved = context.Context!;
        var configDirectory = Path.GetDirectoryName(configStore.GetConfigPath(resolved.UnityProject.RepositoryRoot).Value)!;
        var result = await presetCatalog.ListAsync(resolved.Config, configDirectory, cancellationToken).ConfigureAwait(false);
        var commandResult = ProgramCommandResultFactory.CreatePresetList(resolved.UnityProject, result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}

internal sealed class ProgramPresetsDescribeCommand
{
    private readonly IProjectContextResolver projectContextResolver;
    private readonly IUcliConfigStore configStore;
    private readonly IProgramPresetCatalog presetCatalog;
    private readonly ICommandResultWriter commandResultWriter;

    public ProgramPresetsDescribeCommand (
        IProjectContextResolver projectContextResolver,
        IUcliConfigStore configStore,
        IProgramPresetCatalog presetCatalog,
        ICommandResultWriter commandResultWriter)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        this.presetCatalog = presetCatalog ?? throw new ArgumentNullException(nameof(presetCatalog));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Describes one resolved Program Preset. </summary>
    /// <param name="id">Program preset identifier.</param>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    [Command(UcliCommandNames.DescribeSubcommand)]
    public async Task<int> DescribeAsync (
        string id,
        [AbsolutePathArgumentParser] AbsolutePath? projectPath = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();
        var context = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!context.IsSuccess)
        {
            var error = ProgramCommandResultFactory.CreatePresetDescribeError(context.Error!);
            commandResultWriter.WriteToStandardOutput(error);
            return error.ExitCode;
        }

        var resolved = context.Context!;
        var configDirectory = Path.GetDirectoryName(configStore.GetConfigPath(resolved.UnityProject.RepositoryRoot).Value)!;
        var result = await presetCatalog.ResolveAsync(id, resolved.Config, configDirectory, cancellationToken).ConfigureAwait(false);
        var commandResult = ProgramCommandResultFactory.CreatePresetDescribe(resolved.UnityProject, result);
        commandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
