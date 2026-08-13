using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Features.Screenshot.Capture;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Projects;
using ExecutionMode = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary>
/// Adapts the existing typed Lifecycle Execution handlers to one Program Run.
/// The port owns no Lifecycle state machine: it only fixes Program persistence
/// at the provider's durable-start gate and delegates terminal recovery to the
/// action that owns that Lifecycle Execution kind.
/// </summary>
internal sealed class ProgramLifecycleStepExecutionPort : IProgramStepExecutionPort
{
    private readonly ProgramRunHostContext hostContext;
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly IRefreshService refreshService;
    private readonly IReadyService readyService;
    private readonly IScreenshotCaptureService screenshotCaptureService;
    private readonly ICompileService compileService;
    private readonly IPlayEnterService playEnterService;
    private readonly IPlayExitService playExitService;
    private readonly ProgramCallPreflightService callPreflightService;
    private readonly IProgramArtifactStoreFactory artifactStoreFactory;
    private readonly ILifecycleExecutionReconnectResolver lifecycleReconnectResolver;
    private readonly TimeProvider timeProvider;

    public ProgramLifecycleStepExecutionPort (
        ProgramRunHostContext hostContext,
        IProgramRunStoreFactory storeFactory,
        IReadyService readyService,
        IScreenshotCaptureService screenshotCaptureService,
        IRefreshService refreshService,
        ICompileService compileService,
        IPlayEnterService playEnterService,
        IPlayExitService playExitService,
        ProgramCallPreflightService callPreflightService,
        IProgramArtifactStoreFactory artifactStoreFactory,
        ILifecycleExecutionReconnectResolver lifecycleReconnectResolver,
        TimeProvider timeProvider)
    {
        this.hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.readyService = readyService ?? throw new ArgumentNullException(nameof(readyService));
        this.screenshotCaptureService = screenshotCaptureService ?? throw new ArgumentNullException(nameof(screenshotCaptureService));
        this.refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
        this.compileService = compileService ?? throw new ArgumentNullException(nameof(compileService));
        this.playEnterService = playEnterService ?? throw new ArgumentNullException(nameof(playEnterService));
        this.playExitService = playExitService ?? throw new ArgumentNullException(nameof(playExitService));
        this.callPreflightService = callPreflightService ?? throw new ArgumentNullException(nameof(callPreflightService));
        this.artifactStoreFactory = artifactStoreFactory ?? throw new ArgumentNullException(nameof(artifactStoreFactory));
        this.lifecycleReconnectResolver = lifecycleReconnectResolver ?? throw new ArgumentNullException(nameof(lifecycleReconnectResolver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async ValueTask<ProgramStepExecutionPortResult> StartAsync (
        ProgramStepExecutionStart start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        var step = GetStep(start.Run, start.StepIndex);
        if (step.Command == "ready")
        {
            return await StartReadyAsync(start, cancellationToken).ConfigureAwait(false);
        }
        if (step.Command == "call")
        {
            return await StartCallAsync(start, cancellationToken).ConfigureAwait(false);
        }
        if (step.Command is "screenshot.game" or "screenshot.scene")
        {
            return await StartScreenshotAsync(start, cancellationToken).ConfigureAwait(false);
        }
        var invocation = new LifecycleExecutionStartInvocation(
            new LifecycleExecutionFixedContext(hostContext.Project, ResolveMode(start.Run), hostContext.Binding, start.Run.FixedContext.FailFast),
            CreateDeadline(start.Execution.DeadlineUtc),
            CreateDeadline(start.Execution.DeadlineUtc),
            new ProgramLifecycleStartObserver(storeFactory, hostContext.Project.UnityProject, start));
        return await StartLifecycleAsync(start, invocation, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ProgramStepExecutionRecoveryResult> RecoverAsync (
        ProgramStepExecutionRecovery recovery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        var step = GetStep(recovery.Run, recovery.StepIndex);
        if (step.Command == "call")
        {
            return await RecoverCallAsync(recovery, cancellationToken).ConfigureAwait(false);
        }
        if (step.Command is "ready" or "screenshot.game" or "screenshot.scene")
        {
            return await RecoverSynchronousAsync(recovery, cancellationToken).ConfigureAwait(false);
        }
        if (step.LifecycleExecutionRef is null)
        {
            return ProgramStepExecutionRecoveryResult.TerminallyRecovered(Interrupted("PROGRAM_LIFECYCLE_START_UNAVAILABLE"));
        }

        var invocation = new LifecycleExecutionReconnectInvocation(
            new LifecycleExecutionFixedContext(hostContext.Project, ResolveMode(recovery.Run), hostContext.Binding, recovery.Run.FixedContext.FailFast),
            step.LifecycleExecutionRef,
            recovery.Deadline);
        return await ReconnectLifecycleAsync(recovery, invocation, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ProgramStepExecutionTerminationResult> RequestTerminationAsync (
        ProgramStepExecutionTermination termination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(termination);
        cancellationToken.ThrowIfCancellationRequested();
        var step = GetStep(termination.Run, termination.StepIndex);
        if (step.Execution != termination.Execution)
        {
            return ProgramStepExecutionTerminationResult.CommunicationLost;
        }

        // ready and screenshot complete synchronously in StartAsync. They have
        // no provider-owned operation that can be cancelled or attached to.
        if (step.Command is "ready" or "screenshot.game" or "screenshot.scene")
        {
            return ProgramStepExecutionTerminationResult.Requested;
        }

        // Lifecycle actions retain the cancellation/deadline semantics in
        // their own state machines. Reconnect only the persisted reference;
        // never make a second action request merely to terminate a Program.
        if (step.Command != "call" && step.LifecycleExecutionRef is not null)
        {
            var recovered = await ReconnectLifecycleAsync(
                    new ProgramStepExecutionRecovery(
                        termination.Run,
                        termination.StepIndex,
                        termination.Execution,
                        termination.Deadline,
                        termination.RemainingTimeout),
                    new LifecycleExecutionReconnectInvocation(
                        new LifecycleExecutionFixedContext(hostContext.Project, ResolveMode(termination.Run), hostContext.Binding, termination.Run.FixedContext.FailFast),
                        step.LifecycleExecutionRef,
                        termination.Deadline),
                    cancellationToken)
                .ConfigureAwait(false);
            return recovered.Disposition == ProgramStepExecutionRecoveryDisposition.CommunicationLost
                ? ProgramStepExecutionTerminationResult.CommunicationLost
                : ProgramStepExecutionTerminationResult.Requested;
        }

        return step.Command == "call"
            ? await RequestCallTerminationAsync(termination, step, cancellationToken).ConfigureAwait(false)
            : ProgramStepExecutionTerminationResult.CommunicationLost;
    }

    private async ValueTask<ProgramStepExecutionTerminationResult> RequestCallTerminationAsync (
        ProgramStepExecutionTermination termination,
        ProgramRunStepRecord step,
        CancellationToken cancellationToken)
    {
        var boundary = step.RequestExecution;
        if (boundary is null || boundary.ExecutionId != termination.Execution.ExecutionId)
        {
            return ProgramStepExecutionTerminationResult.CommunicationLost;
        }

        var reason = termination.ReasonCode == "PROGRAM_RUN_CANCELLED"
            ? IpcProgramRequestCancellationReason.UserCancelled
            : IpcProgramRequestCancellationReason.DeadlineExceeded;
        var execution = await hostContext.Binding.ExecuteAsync(
                UcliCommandIds.ProgramRun,
                new UnityRequestPayload.ProgramRequestCancel(new IpcProgramRequestCancelRequest(
                    boundary.ExecutionId,
                    CreateCallBinding(termination.Run, boundary),
                    reason)),
                termination.Deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!execution.IsSuccess || execution.Response!.Errors.Count != 0
            || !IpcPayloadCodec.TryDeserialize(execution.Response.Payload, out IpcProgramRequestCancelResponse response, out _)
            || response.ExecutionId != boundary.ExecutionId
            || response.Reason != reason)
        {
            return ProgramStepExecutionTerminationResult.CommunicationLost;
        }

        return response.Status is IpcProgramRequestCancellationStatus.Requested or IpcProgramRequestCancellationStatus.Terminal
            ? ProgramStepExecutionTerminationResult.Requested
            : ProgramStepExecutionTerminationResult.CommunicationLost;
    }

    private async ValueTask<ProgramStepExecutionPortResult> StartLifecycleAsync (
        ProgramStepExecutionStart start,
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken)
    {
        var step = GetStep(start.Run, start.StepIndex);
        return step.Command switch
        {
            "refresh" => await ToStartAsync(start, await refreshService.StartAsync(start.Execution.ExecutionId, invocation, failFast: start.Run.FixedContext.FailFast, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
            "compile" => await ToStartAsync(start, await compileService.StartAsync(invocation, cancellationToken: cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
            "play.enter" => await ToStartAsync(start, await playEnterService.StartAsync(invocation, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
            "play.exit" => await ToStartAsync(start, await playExitService.StartAsync(invocation, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(step.Command), step.Command, "Program Lifecycle dispatch requires a lifecycle command."),
        };
    }

    private async ValueTask<ProgramStepExecutionPortResult> StartCallAsync (
        ProgramStepExecutionStart start,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareCallAsync(start, cancellationToken).ConfigureAwait(false);
        if (prepared is null)
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(Interrupted("PROGRAM_CALL_PREPARATION_INTERRUPTED"));
        }
        if (prepared.ErrorCode is not null)
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(LocalPreflightFailed(prepared.ErrorCode));
        }
        if (prepared.AttachOnly)
        {
            var attach = await hostContext.Binding.ExecuteAsync(
                    UcliCommandIds.ProgramRun,
                    new UnityRequestPayload.ProgramRequestAttach(new IpcProgramRequestAttachRequest(start.Execution.ExecutionId, prepared.Binding!)),
                    CreateDeadline(start.Execution.DeadlineUtc),
                    cancellationToken)
                .ConfigureAwait(false);
            return await ToCallStartResultAsync(attach, prepared.Run, start.StepIndex, start.Execution.ExecutionId, cancellationToken).ConfigureAwait(false);
        }
        var execution = await hostContext.Binding.ExecuteAsync(
                UcliCommandIds.ProgramRun,
                new UnityRequestPayload.ProgramRequestStart(new IpcProgramRequestStartRequest(
                    start.Execution.ExecutionId,
                    prepared.Binding!,
                    prepared.Request!)),
                CreateDeadline(start.Execution.DeadlineUtc),
                cancellationToken)
            .ConfigureAwait(false);
        return await ToCallStartResultAsync(execution, prepared.Run, start.StepIndex, start.Execution.ExecutionId, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ProgramStepExecutionRecoveryResult> RecoverCallAsync (
        ProgramStepExecutionRecovery recovery,
        CancellationToken cancellationToken)
    {
        var step = GetStep(recovery.Run, recovery.StepIndex);
        var boundary = step.RequestExecution;
        if (boundary is null || boundary.ExecutionId != recovery.Execution.ExecutionId)
        {
            return ProgramStepExecutionRecoveryResult.TerminallyRecovered(Interrupted("PROGRAM_CALL_BOUNDARY_UNAVAILABLE"));
        }
        var binding = CreateCallBinding(recovery.Run, boundary);
        var execution = await hostContext.Binding.ExecuteAsync(
                UcliCommandIds.ProgramRun,
                new UnityRequestPayload.ProgramRequestAttach(new IpcProgramRequestAttachRequest(boundary.ExecutionId, binding)),
                recovery.Deadline,
                cancellationToken)
            .ConfigureAwait(false);
        return await ToCallRecoveryResultAsync(execution, recovery.Run, recovery.StepIndex, boundary.ExecutionId, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ProgramStepExecutionRecoveryResult> RecoverSynchronousAsync (
        ProgramStepExecutionRecovery recovery,
        CancellationToken cancellationToken)
    {
        var step = GetStep(recovery.Run, recovery.StepIndex);
        if (step.StepResultRef is null || step.GenerationBefore is null || step.GenerationAfter is null)
        {
            return ProgramStepExecutionRecoveryResult.TerminallyRecovered(
                Interrupted("PROGRAM_SYNCHRONOUS_STEP_RESULT_UNAVAILABLE"));
        }

        var observedGeneration = await ObserveProgramGenerationAsync(recovery.Run, recovery.Deadline, cancellationToken).ConfigureAwait(false);
        if (observedGeneration is null
            || observedGeneration != step.GenerationBefore
            || observedGeneration != step.GenerationAfter)
        {
            return ProgramStepExecutionRecoveryResult.TerminallyRecovered(
                Interrupted("PROGRAM_GENERATION_MISMATCH"));
        }

        var bytes = await artifactStoreFactory.ForProject(hostContext.Project.UnityProject)
            .ReadAsync(step.StepResultRef, cancellationToken)
            .ConfigureAwait(false);
        if (bytes is null)
        {
            return ProgramStepExecutionRecoveryResult.TerminallyRecovered(
                Interrupted("PROGRAM_SYNCHRONOUS_STEP_RESULT_UNAVAILABLE"));
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            var terminal = step.Command == "ready"
                ? DecodeReadyTerminal(document.RootElement, observedGeneration)
                : DecodeScreenshotTerminal(document.RootElement, observedGeneration);
            return ProgramStepExecutionRecoveryResult.TerminallyRecovered(terminal);
        }
        catch (JsonException)
        {
            return ProgramStepExecutionRecoveryResult.TerminallyRecovered(
                Interrupted("PROGRAM_SYNCHRONOUS_STEP_RESULT_CONTRACT_INVALID"));
        }
    }

    private static ProgramStepExecutionRecoveredTerminal DecodeReadyTerminal (
        JsonElement result,
        UnityEditorGenerationSnapshot generation)
    {
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("verdict", out var verdict)
            || !result.TryGetProperty("generation", out var storedGeneration)
            || verdict.ValueKind != JsonValueKind.String)
        {
            return Interrupted("PROGRAM_SYNCHRONOUS_STEP_RESULT_CONTRACT_INVALID");
        }
        var decodedVerdict = verdict.GetString() switch
        {
            "pass" => Verdict.Pass,
            "fail" => Verdict.Fail,
            "incomplete" => Verdict.Incomplete,
            _ => default(Verdict?),
        };
        if (!decodedVerdict.HasValue)
        {
            return Interrupted("PROGRAM_SYNCHRONOUS_STEP_RESULT_CONTRACT_INVALID");
        }
        var decodedGeneration = JsonSerializer.Deserialize<UnityEditorGenerationSnapshot>(
            storedGeneration.GetRawText(), IpcJsonSerializerOptions.Default);
        if (decodedGeneration != generation)
        {
            return Interrupted("PROGRAM_SYNCHRONOUS_STEP_RESULT_CONTRACT_INVALID");
        }
        return Completed(decodedVerdict.Value, ExecutionApplicationState.NotApplied, generation);
    }

    private static ProgramStepExecutionRecoveredTerminal DecodeScreenshotTerminal (
        JsonElement result,
        UnityEditorGenerationSnapshot generation)
    {
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("capture", out var capture)
            || !result.TryGetProperty("failure", out var failure))
        {
            return Interrupted("PROGRAM_SYNCHRONOUS_STEP_RESULT_CONTRACT_INVALID");
        }
        if (capture.ValueKind == JsonValueKind.Object && failure.ValueKind == JsonValueKind.Null)
        {
            return Completed(null, ExecutionApplicationState.NotApplied, generation);
        }
        if (capture.ValueKind != JsonValueKind.Null
            || failure.ValueKind != JsonValueKind.Object
            || !failure.TryGetProperty("errorCode", out var errorCode)
            || errorCode.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(errorCode.GetString()))
        {
            return Interrupted("PROGRAM_SYNCHRONOUS_STEP_RESULT_CONTRACT_INVALID");
        }
        return Failed(ExecutionApplicationState.NotApplied, errorCode.GetString()!, generation);
    }

    private async ValueTask<PreparedCall?> PrepareCallAsync (ProgramStepExecutionStart start, CancellationToken cancellationToken)
    {
        var store = storeFactory.ForProject(hostContext.Project.UnityProject);
        while (true)
        {
            var current = await store.ReadAsync(start.Run.RunId, cancellationToken).ConfigureAwait(false);
            if (current is null || current.Cursor != start.StepIndex || current.Steps[start.StepIndex].Execution != start.Execution)
            {
                return null;
            }
            var currentStep = current.Steps[start.StepIndex];
            if (currentStep.RequestExecution is not null)
            {
                if (currentStep.RequestExecution.ExecutionId != start.Execution.ExecutionId)
                {
                    return null;
                }
                return new PreparedCall(
                    current,
                    CreateCallBinding(current, currentStep.RequestExecution),
                    null,
                    true);
            }
            var request = await LoadCallRequestAsync(store, current, start.StepIndex, cancellationToken).ConfigureAwait(false);
            if (request is null)
            {
                return null;
            }
            var preflight = await callPreflightService.PrepareAsync(
                    current,
                    hostContext.Project,
                    hostContext.Binding,
                    request.Document,
                    CreateDeadline(start.Execution.DeadlineUtc),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!preflight.IsSuccess)
            {
                return new PreparedCall(current, null, null, false, preflight.ErrorCode);
            }
            var facts = preflight.Preflight!;
            var boundary = new ProgramRequestExecutionBoundary(
                start.Execution.ExecutionId,
                current.Project,
                facts.Host,
                facts.Generation,
                facts.RequestDigest,
                facts.RequestPlanRef,
                facts.PlanTokenDigest,
                facts.Descriptors.Select(static item => item.Artifact).ToArray(),
                facts.Descriptors.Select(static item => item.Digest).ToArray(),
                start.Execution.StartedAtUtc,
                start.Execution.DeadlineUtc);
            var steps = current.Steps.Select((candidate, index) => index == start.StepIndex
                ? candidate with
                {
                    GenerationBefore = facts.Generation,
                    RequestPlanRef = facts.RequestPlanRef,
                    OperationDescriptorRefs = facts.Descriptors.Select(static item => item.Artifact).ToArray(),
                    RequestExecution = boundary,
                    ExecutionPortInvoked = true,
                }
                : candidate).ToArray();
            var exchange = await store.CompareExchangeAsync(current, CopyRun(current, steps), cancellationToken).ConfigureAwait(false);
            if (exchange.Exchanged)
            {
                return new PreparedCall(exchange.Current, CreateCallBinding(exchange.Current, boundary), facts.Request, false);
            }
        }
    }

    private static IpcProgramRequestExecutionBinding CreateCallBinding (
        ProgramRunRecord run,
        ProgramRequestExecutionBoundary boundary) => new(
            run.Project,
            boundary.Host,
            boundary.StartedGeneration,
            boundary.DeadlineUtc,
            boundary.RequestDigest,
            boundary.RequestPlanRef.Digest,
            boundary.PlanTokenDigest,
            boundary.OperationDescriptorDigests,
            Sha256Digest.Parse(run.FixedContext.Authorization.Digest),
            run.FixedContext.Configuration.Digest);

    private async ValueTask<ProgramStepExecutionPortResult> ToCallStartResultAsync (UnityRequestExecutionResult execution, ProgramRunRecord run, int stepIndex, Guid executionId, CancellationToken cancellationToken)
    {
        if (!execution.IsSuccess)
        {
            return ProgramStepExecutionPortResult.CommunicationLost;
        }
        return await ToCallTerminalAsync(execution.Response!, run, stepIndex, executionId, cancellationToken).ConfigureAwait(false) is { } terminal
            ? ProgramStepExecutionPortResult.TerminallyReturned(terminal)
            : ProgramStepExecutionPortResult.CommunicationLost;
    }

    private async ValueTask<ProgramStepExecutionRecoveryResult> ToCallRecoveryResultAsync (UnityRequestExecutionResult execution, ProgramRunRecord run, int stepIndex, Guid executionId, CancellationToken cancellationToken)
    {
        if (!execution.IsSuccess)
        {
            return ProgramStepExecutionRecoveryResult.CommunicationLost;
        }
        return await ToCallTerminalAsync(execution.Response!, run, stepIndex, executionId, cancellationToken).ConfigureAwait(false) is { } terminal
            ? ProgramStepExecutionRecoveryResult.TerminallyRecovered(terminal)
            : ProgramStepExecutionRecoveryResult.CommunicationLost;
    }

    private async ValueTask<ProgramStepExecutionRecoveredTerminal?> ToCallTerminalAsync (UnityRequestResponse response, ProgramRunRecord run, int stepIndex, Guid executionId, CancellationToken cancellationToken)
    {
        var boundary = stepIndex >= 0 && stepIndex < run.Steps.Count ? run.Steps[stepIndex].RequestExecution : null;
        if (response.Errors.Count != 0
            || !IpcPayloadCodec.TryDeserialize(response.Payload, out IpcProgramRequestExecutionResponse programResponse, out _)
            || programResponse.ExecutionId != executionId
            || boundary is null
            || programResponse.Host != boundary.Host)
        {
            return Interrupted("PROGRAM_CALL_RESPONSE_CONTRACT_INVALID");
        }
        if (programResponse.Status == IpcProgramRequestExecutionStatus.GenerationMismatch)
        {
            return Interrupted("PROGRAM_GENERATION_MISMATCH");
        }
        if (programResponse.Generation != boundary.StartedGeneration)
        {
            return Interrupted("PROGRAM_CALL_RESPONSE_CONTRACT_INVALID");
        }
        return programResponse.Status switch
        {
            IpcProgramRequestExecutionStatus.Running => null,
            IpcProgramRequestExecutionStatus.Terminal => await DecodeCallTerminalAsync(run, stepIndex, programResponse, cancellationToken).ConfigureAwait(false),
            IpcProgramRequestExecutionStatus.Conflict => Interrupted("PROGRAM_CALL_EXECUTION_CONFLICT"),
            IpcProgramRequestExecutionStatus.GenerationMismatch => throw new InvalidOperationException("Generation mismatch status must be handled before generation equality is required."),
            IpcProgramRequestExecutionStatus.NotStarted or IpcProgramRequestExecutionStatus.Unavailable => Interrupted("PROGRAM_CALL_EXECUTION_UNAVAILABLE"),
            _ => Interrupted("PROGRAM_CALL_RESPONSE_CONTRACT_INVALID"),
        };
    }

    private async ValueTask<ProgramStepExecutionRecoveredTerminal> DecodeCallTerminalAsync (ProgramRunRecord run, int stepIndex, IpcProgramRequestExecutionResponse response, CancellationToken cancellationToken)
    {
        try
        {
            var inner = JsonSerializer.Deserialize<IpcResponse>(response.ResponseBytes!, IpcJsonSerializerOptions.Default);
            if (inner is null)
            {
                return Interrupted("PROGRAM_CALL_RESPONSE_CONTRACT_INVALID");
            }
            var converted = ExecuteResponseConverter.Convert(new UnityRequestResponse(inner.Payload, inner.Errors.Select(static error => new OperationExecutionError(error.Code, error.Message, error.InstancePath)).ToArray()), hostContext.Project.UnityProject);
            if (!await TryValidateCallResultAsync(run, stepIndex, converted, cancellationToken).ConfigureAwait(false))
            {
                await PersistRejectedCallResultArtifactAsync(run, stepIndex, response.ResponseBytes!, cancellationToken).ConfigureAwait(false);
                return Interrupted("PROGRAM_OPERATION_RESULT_CONTRACT_INVALID");
            }
            if (!await PersistCallResultArtifactAsync(run, stepIndex, response.ResponseBytes!, cancellationToken).ConfigureAwait(false))
            {
                return Interrupted("PROGRAM_CALL_RESULT_ARTIFACT_UNAVAILABLE");
            }
            if (inner.Status != IpcResponseStatus.Ok || inner.Errors.Count != 0 || !converted.IsSuccess)
            {
                return Failed(ExecutionApplicationState.Indeterminate, "PROGRAM_CALL_FAILED", response.Generation);
            }
            return Completed(AggregateVerdict(converted.OpResults), DeriveApplicationState(converted.OpResults), response.Generation);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException)
        {
            return Interrupted("PROGRAM_CALL_RESPONSE_CONTRACT_INVALID");
        }
    }

    private async ValueTask<bool> PersistCallResultArtifactAsync (ProgramRunRecord run, int stepIndex, byte[] bytes, CancellationToken cancellationToken)
    {
        var artifact = await artifactStoreFactory.ForProject(hostContext.Project.UnityProject).PublishAsync(
                run.RunId,
                ProgramTerminalArtifactContract.RequestResultKind,
                ProgramTerminalArtifactContract.JsonMediaType,
                bytes,
                cancellationToken)
            .ConfigureAwait(false);
        var store = storeFactory.ForProject(hostContext.Project.UnityProject);
        while (true)
        {
            var current = await store.ReadAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            if (current is null || current.Cursor != stepIndex || current.Steps[stepIndex].RequestExecution?.ExecutionId != run.Steps[stepIndex].Execution?.ExecutionId)
            {
                return false;
            }
            var step = current.Steps[stepIndex];
            if (step.StepResultRef is not null)
            {
                return step.StepResultRef.Digest == artifact.Digest;
            }
            var steps = current.Steps.Select((candidate, index) => index == stepIndex
                ? candidate with { StepResultRef = artifact, ArtifactRefs = candidate.ArtifactRefs.Append(artifact).ToArray() }
                : candidate).ToArray();
            var exchange = await store.CompareExchangeAsync(current, CopyRun(current, steps), cancellationToken).ConfigureAwait(false);
            if (exchange.Exchanged)
            {
                return true;
            }
        }
    }

    private async ValueTask PersistRejectedCallResultArtifactAsync (ProgramRunRecord run, int stepIndex, byte[] bytes, CancellationToken cancellationToken)
    {
        var artifact = await artifactStoreFactory.ForProject(hostContext.Project.UnityProject).PublishAsync(
                run.RunId,
                ProgramTerminalArtifactContract.RejectedStepResultKind,
                ProgramTerminalArtifactContract.JsonMediaType,
                bytes,
                cancellationToken)
            .ConfigureAwait(false);
        var store = storeFactory.ForProject(hostContext.Project.UnityProject);
        while (true)
        {
            var current = await store.ReadAsync(run.RunId, cancellationToken).ConfigureAwait(false);
            if (current is null || current.Cursor != stepIndex)
            {
                return;
            }
            var step = current.Steps[stepIndex];
            if (step.ArtifactRefs.Any(existing => existing.Digest == artifact.Digest))
            {
                return;
            }
            var steps = current.Steps.Select((candidate, index) => index == stepIndex
                ? candidate with { ArtifactRefs = candidate.ArtifactRefs.Append(artifact).ToArray() }
                : candidate).ToArray();
            var exchange = await store.CompareExchangeAsync(current, CopyRun(current, steps), cancellationToken).ConfigureAwait(false);
            if (exchange.Exchanged)
            {
                return;
            }
        }
    }

    private async ValueTask<bool> TryValidateCallResultAsync (
        ProgramRunRecord run,
        int stepIndex,
        ExecuteResponseConversionResult response,
        CancellationToken cancellationToken)
    {
        if (stepIndex < 0 || stepIndex >= run.Steps.Count)
        {
            return false;
        }
        var boundary = run.Steps[stepIndex].RequestExecution;
        if (boundary is null)
        {
            return false;
        }

        var descriptors = new Dictionary<string, UcliOperationDescriptor>(StringComparer.Ordinal);
        for (var index = 0; index < boundary.OperationDescriptorRefs.Count; index++)
        {
            var bytes = await artifactStoreFactory.ForProject(hostContext.Project.UnityProject).ReadAsync(boundary.OperationDescriptorRefs[index], cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return false;
            }

            var descriptor = JsonSerializer.Deserialize<UcliOperationDescriptor>(bytes, IpcJsonSerializerOptions.Default);
            if (descriptor is null || descriptor.DescriptorDigest != boundary.OperationDescriptorDigests[index])
            {
                return false;
            }

            descriptors.Add(descriptor.Name, descriptor);
        }
        var request = await LoadCallRequestAsync(storeFactory.ForProject(hostContext.Project.UnityProject), run, stepIndex, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return false;
        }

        var parser = new ValidateRequestJsonParser();
        var parsed = parser.Parse(JsonSerializer.Serialize(request.Document, IpcJsonSerializerOptions.Default));
        return parsed.IsSuccess && OperationExecutionResultContractValidator.TryValidate(
            parsed.Request! with { AllowPlayMode = run.FixedContext.Authorization.AllowPlayMode },
            descriptors,
            IpcExecuteOperationPhase.Call,
            response,
            out _);
    }

    private static Verdict? AggregateVerdict (IReadOnlyList<OperationExecutionOperationResult> results)
    {
        if (results.Any(static result => result.Verdict == Verdict.Fail))
        {
            return Verdict.Fail;
        }

        if (results.Any(static result => result.Verdict == Verdict.Incomplete))
        {
            return Verdict.Incomplete;
        }

        return results.Any(static result => result.Verdict == Verdict.Pass) ? Verdict.Pass : null;
    }

    private static ExecutionApplicationState DeriveApplicationState (IReadOnlyList<OperationExecutionOperationResult> results)
    {
        if (results.Any(static result => result.Applied))
        {
            return ExecutionApplicationState.Applied;
        }

        return results.All(static result => !result.Changed) ? ExecutionApplicationState.NotApplied : ExecutionApplicationState.Indeterminate;
    }

    private static async ValueTask<CallRequest?> LoadCallRequestAsync (
        IProgramRunStore store,
        ProgramRunRecord run,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        var stored = await store.ReadDefinitionAsync(run.RunId, cancellationToken).ConfigureAwait(false);
        if (stored is null || stepIndex >= stored.Definition.Steps.Count || stored.Definition.Steps[stepIndex] is not CallProgramStep)
        {
            return null;
        }
        JsonElement requestDocument;
        // The canonical Program snapshot is the authoritative request source.
        // Inline calls carry their request in the Program document; referenced
        // calls were restored into the source manifest at this same index.
        var fixedDefinition = stored.Definition;
        if (fixedDefinition.Steps[stepIndex] is InlineCallProgramStep inline)
        {
            requestDocument = JsonSerializer.SerializeToElement(inline.Request, IpcJsonSerializerOptions.Default);
        }
        else
        {
            var source = fixedDefinition.Sources.SingleOrDefault(item => item.InstancePath == $"/steps/{stepIndex}/requestPath");
            if (source is null)
            {
                return null;
            }
            using var document = JsonDocument.Parse(source.CanonicalDocumentJson);
            requestDocument = document.RootElement.Clone();
        }
        var canonical = JsonSerializer.SerializeToUtf8Bytes(requestDocument, IpcJsonSerializerOptions.Default);
        using var arguments = JsonDocument.Parse(canonical);
        return new CallRequest(arguments.RootElement.Clone());
    }

    private sealed record CallRequest (JsonElement Document);

    private sealed record PreparedCall (ProgramRunRecord Run, IpcProgramRequestExecutionBinding? Binding, IpcExecuteRequest? Request, bool AttachOnly, string? ErrorCode = null);

    private async ValueTask<ProgramStepExecutionPortResult> StartReadyAsync (
        ProgramStepExecutionStart start,
        CancellationToken cancellationToken)
    {
        var observation = await readyService.ObserveOnFixedHostAsync(
                hostContext.Project,
                hostContext.Binding,
                CreateDeadline(start.Execution.DeadlineUtc),
                cancellationToken)
            .ConfigureAwait(false);
        if (observation.Failure is not null)
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(
                Interrupted("PROGRAM_READY_OBSERVATION_UNAVAILABLE"));
        }
        if (observation.Generation is null
            || !observation.Verdict.HasValue
            || observation.IsReady != (observation.Verdict == Verdict.Pass))
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(
                Interrupted("PROGRAM_READY_RESULT_CONTRACT_INVALID"));
        }
        if (observation.Generation != ExpectedSynchronousGeneration(start.Run))
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(
                Interrupted("PROGRAM_GENERATION_MISMATCH"));
        }
        var resultBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            verdict = observation.Verdict,
            generation = observation.Generation,
        }, IpcJsonSerializerOptions.Default);
        var generation = observation.Generation;
        var persisted = await PersistSynchronousFactsAsync(
                start,
                [],
                resultBytes,
                generation,
                observation.Generation,
                cancellationToken)
            .ConfigureAwait(false);
        if (!persisted)
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(Interrupted("PROGRAM_STEP_PERSISTENCE_CONFLICT"));
        }
        return ProgramStepExecutionPortResult.TerminallyReturned(
            Completed(observation.Verdict, ExecutionApplicationState.NotApplied, observation.Generation));
    }

    private async ValueTask<ProgramStepExecutionPortResult> StartScreenshotAsync (
        ProgramStepExecutionStart start,
        CancellationToken cancellationToken)
    {
        var stored = await storeFactory.ForProject(hostContext.Project.UnityProject)
            .ReadDefinitionAsync(start.Run.RunId, cancellationToken).ConfigureAwait(false);
        if (stored is null || start.StepIndex >= stored.Definition.Steps.Count)
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(Interrupted("PROGRAM_DEFINITION_UNAVAILABLE"));
        }
        var definition = stored.Definition.Steps[start.StepIndex];
        var target = definition switch
        {
            ScreenshotGameProgramStep => IpcScreenshotTarget.Game,
            ScreenshotSceneProgramStep => IpcScreenshotTarget.Scene,
            _ => throw new InvalidOperationException("Program Step definition does not match its persisted command."),
        };
        PixelDimensions? dimensions = definition is ScreenshotGameProgramStep { Width: not null, Height: not null } game
            ? new PixelDimensions(game.Width.Value, game.Height.Value)
            : null;
        var before = await ObserveProgramGenerationAsync(start.Run, CreateDeadline(start.Execution.DeadlineUtc), cancellationToken).ConfigureAwait(false);
        if (before is null || before != ExpectedSynchronousGeneration(start.Run))
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(Interrupted("PROGRAM_GENERATION_MISMATCH"));
        }

        var result = await screenshotCaptureService.CaptureOnFixedHostAsync(
                hostContext.Project,
                hostContext.Binding,
                target,
                dimensions,
                CreateDeadline(start.Execution.DeadlineUtc),
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess && result.FailureDisposition != ScreenshotCaptureFailureDisposition.Terminal)
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(Interrupted(
                result.FailureDisposition == ScreenshotCaptureFailureDisposition.ContractInvalid
                    ? "PROGRAM_SCREENSHOT_RESULT_CONTRACT_INVALID"
                    : "PROGRAM_SCREENSHOT_COMMUNICATION_LOST"));
        }
        var responseGeneration = result.IsSuccess
            ? result.Output!.Capture.State.Generations
            : await ObserveProgramGenerationAsync(start.Run, CreateDeadline(start.Execution.DeadlineUtc), cancellationToken).ConfigureAwait(false);
        var after = await ObserveProgramGenerationAsync(start.Run, CreateDeadline(start.Execution.DeadlineUtc), cancellationToken).ConfigureAwait(false);
        if (responseGeneration is null
            || after is null
            || responseGeneration != before
            || after != responseGeneration
            || result.FailureDisposition != ScreenshotCaptureFailureDisposition.Terminal)
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(Interrupted(
                result.FailureDisposition == ScreenshotCaptureFailureDisposition.ContractInvalid
                    ? "PROGRAM_SCREENSHOT_RESULT_CONTRACT_INVALID"
                    : result.FailureDisposition == ScreenshotCaptureFailureDisposition.CommunicationLost
                        ? "PROGRAM_SCREENSHOT_COMMUNICATION_LOST"
                        : "PROGRAM_GENERATION_MISMATCH"));
        }

        var resultBytes = result.IsSuccess
            ? JsonSerializer.SerializeToUtf8Bytes(ProgramScreenshotStepResult.Success(result.Output!), IpcJsonSerializerOptions.Default)
            : JsonSerializer.SerializeToUtf8Bytes(ProgramScreenshotStepResult.FromFailure(
                result.Error ?? throw new InvalidOperationException("Screenshot failure did not provide its error.")), IpcJsonSerializerOptions.Default);
        var artifacts = result.IsSuccess ? new ArtifactRef[] { result.Output!.Artifact } : [];
        var persisted = await PersistSynchronousFactsAsync(
                start,
                artifacts,
                resultBytes,
                before,
                after,
                cancellationToken)
            .ConfigureAwait(false);
        if (!persisted)
        {
            return ProgramStepExecutionPortResult.TerminallyReturned(Interrupted("PROGRAM_STEP_PERSISTENCE_CONFLICT"));
        }

        return result.IsSuccess
            ? ProgramStepExecutionPortResult.TerminallyReturned(Completed(null, ExecutionApplicationState.NotApplied, after))
            : ProgramStepExecutionPortResult.TerminallyReturned(Failed(
                ExecutionApplicationState.NotApplied,
                result.Error!.Code?.Value ?? "PROGRAM_SCREENSHOT_FAILED",
                after));
    }

    private async ValueTask<bool> PersistSynchronousFactsAsync (
        ProgramStepExecutionStart start,
        IReadOnlyList<ArtifactRef> artifactRefs,
        ReadOnlyMemory<byte> stepResult,
        UnityEditorGenerationSnapshot generationBefore,
        UnityEditorGenerationSnapshot? generationAfter,
        CancellationToken cancellationToken)
    {
        var stepResultArtifact = await artifactStoreFactory.ForProject(hostContext.Project.UnityProject).PublishAsync(
                start.Run.RunId,
                ProgramTerminalArtifactContract.StepResultKind,
                ProgramTerminalArtifactContract.JsonMediaType,
                stepResult,
                cancellationToken)
            .ConfigureAwait(false);
        var store = storeFactory.ForProject(hostContext.Project.UnityProject);
        while (true)
        {
            var current = await store.ReadAsync(start.Run.RunId, cancellationToken).ConfigureAwait(false);
            if (current is null || current.Cursor != start.StepIndex || current.Steps[start.StepIndex].Execution != start.Execution)
            {
                return false;
            }
            var step = current.Steps[start.StepIndex];
            var steps = current.Steps.Select((candidate, index) => index == start.StepIndex
                ? candidate with
                {
                    StepResultRef = stepResultArtifact,
                    ArtifactRefs = artifactRefs.Append(stepResultArtifact).ToArray(),
                    GenerationBefore = generationBefore,
                    GenerationAfter = generationAfter,
                }
                : candidate).ToArray();
            var replacement = CopyRun(current, steps);
            var exchange = await store.CompareExchangeAsync(current, replacement, cancellationToken).ConfigureAwait(false);
            if (exchange.Exchanged)
            {
                return true;
            }
        }
    }

    /// <summary>
    /// Reads the generation from the Program's already fixed host. A screenshot
    /// Step cannot use a later host or infer its terminal generation from a
    /// request transport result.
    /// </summary>
    private async ValueTask<UnityEditorGenerationSnapshot?> ObserveProgramGenerationAsync (
        ProgramRunRecord run,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var execution = await hostContext.Binding.ExecuteAsync(
                UcliCommandIds.ProgramRun,
                new UnityRequestPayload.ProgramExecutionContext(CreateEffectiveAuthorization(run)),
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!execution.IsSuccess || execution.Response!.Errors.Count != 0)
        {
            return null;
        }
        if (!IpcPayloadCodec.TryDeserialize(execution.Response.Payload, out IpcProgramExecutionContextResponse response, out _)
            || !ProgramRunRecord.HasSameProgramFixedHost(run.Host, response.Host))
        {
            return null;
        }
        return response.Generation;
    }

    private static IpcProgramEffectiveAuthorizationSnapshot CreateEffectiveAuthorization (ProgramRunRecord run)
    {
        var authorization = run.FixedContext.Authorization;
        return new IpcProgramEffectiveAuthorizationSnapshot(
            authorization.AllowDangerous,
            authorization.AllowPlayMode,
            Sha256Digest.Parse(authorization.Digest));
    }

    private UnityEditorGenerationSnapshot ExpectedSynchronousGeneration (ProgramRunRecord run) =>
        run.CurrentEditorGeneration ?? hostContext.Generation;

    private static ProgramRunRecord CopyRun (ProgramRunRecord current, IReadOnlyList<ProgramRunStepRecord> steps) => new(
        current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest, current.DefinitionSnapshotRef,
        current.Project, current.FixedContext, current.Host, current.StartedGeneration, current.CurrentEditorGeneration,
        current.DeadlineUtc, current.StartedAtUtc, current.UpdatedAtUtc, current.State, current.Cursor,
        steps, current.ChildExecutionRefs, current.Cancellation, current.TerminalRecordRef)
    {
        SupervisorObservation = current.SupervisorObservation,
        HostObservation = current.HostObservation,
        TerminalReasonCode = current.TerminalReasonCode,
    };

    private async ValueTask<ProgramStepExecutionRecoveryResult> ReconnectLifecycleAsync (
        ProgramStepExecutionRecovery recovery,
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken)
    {
        var step = GetStep(recovery.Run, recovery.StepIndex);
        return step.Command switch
        {
            "refresh" => await ToRecoveryAsync(recovery, await refreshService.ReconnectAsync(recovery.Execution.ExecutionId, invocation, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
            "compile" => await ToRecoveryAsync(recovery, await compileService.ReconnectAsync(invocation, cancellationToken: cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
            "play.enter" => await ToRecoveryAsync(recovery, await playEnterService.ReconnectAsync(invocation, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
            "play.exit" => await ToRecoveryAsync(recovery, await playExitService.ReconnectAsync(invocation, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(step.Command), step.Command, "Program Lifecycle recovery requires a lifecycle command."),
        };
    }

    private async ValueTask<ProgramStepExecutionPortResult> ToStartAsync (
        ProgramStepExecutionStart start,
        RefreshExecutionResult result,
        CancellationToken cancellationToken) => await ProjectLifecycleStartAsync(
        start,
        result.IsSuccess ? result.Output!.LifecycleExecutionRef as ExecutionRef : result.ErrorOutput?.LifecycleExecutionRef,
        result.ErrorOutput?.ApplicationState ?? ExecutionApplicationState.NotApplied,
        result.Failures.FirstOrDefault()?.Code.Value,
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<ProgramStepExecutionPortResult> ToStartAsync (
        ProgramStepExecutionStart start,
        CompileExecutionResult result,
        CancellationToken cancellationToken) => await ProjectLifecycleStartAsync(
        start,
        result is CompileExecutionResult.CompletedResult completed ? completed.Output.LifecycleExecutionRef as ExecutionRef : ((CompileExecutionResult.FailedResult)result).LifecycleExecutionRef,
        result is CompileExecutionResult.FailedResult failed ? failed.ApplicationState : ExecutionApplicationState.NotApplied,
        result is CompileExecutionResult.FailedResult failure ? failure.Failure.Code.Value : null,
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<ProgramStepExecutionPortResult> ToStartAsync (
        ProgramStepExecutionStart start,
        PlayEnterExecutionResult result,
        CancellationToken cancellationToken) => await ProjectLifecycleStartAsync(
        start,
        result.IsSuccess ? result.Output!.LifecycleExecutionRef as ExecutionRef : result.FailureContext?.LifecycleExecutionRef,
        result.FailureContext?.ApplicationState ?? ExecutionApplicationState.NotApplied,
        result.Error?.Code.Value,
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<ProgramStepExecutionPortResult> ToStartAsync (
        ProgramStepExecutionStart start,
        PlayExitExecutionResult result,
        CancellationToken cancellationToken) => await ProjectLifecycleStartAsync(
        start,
        result.IsSuccess ? result.Output!.LifecycleExecutionRef as ExecutionRef : result.FailureContext?.LifecycleExecutionRef,
        result.FailureContext?.ApplicationState ?? ExecutionApplicationState.NotApplied,
        result.Error?.Code.Value,
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<ProgramStepExecutionRecoveryResult> ToRecoveryAsync (
        ProgramStepExecutionRecovery recovery,
        RefreshExecutionResult result,
        CancellationToken cancellationToken) => await ProjectLifecycleRecoveryAsync(
        recovery,
        result.IsSuccess ? result.Output!.LifecycleExecutionRef as ExecutionRef : result.ErrorOutput?.LifecycleExecutionRef,
        result.ErrorOutput?.ApplicationState ?? ExecutionApplicationState.NotApplied,
        result.Failures.FirstOrDefault()?.Code.Value,
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<ProgramStepExecutionRecoveryResult> ToRecoveryAsync (
        ProgramStepExecutionRecovery recovery,
        CompileExecutionResult result,
        CancellationToken cancellationToken) => await ProjectLifecycleRecoveryAsync(
        recovery,
        result is CompileExecutionResult.CompletedResult completed ? completed.Output.LifecycleExecutionRef as ExecutionRef : ((CompileExecutionResult.FailedResult)result).LifecycleExecutionRef,
        result is CompileExecutionResult.FailedResult failed ? failed.ApplicationState : ExecutionApplicationState.NotApplied,
        result is CompileExecutionResult.FailedResult failure ? failure.Failure.Code.Value : null,
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<ProgramStepExecutionRecoveryResult> ToRecoveryAsync (
        ProgramStepExecutionRecovery recovery,
        PlayEnterExecutionResult result,
        CancellationToken cancellationToken) => await ProjectLifecycleRecoveryAsync(
        recovery,
        result.IsSuccess ? result.Output!.LifecycleExecutionRef as ExecutionRef : result.FailureContext?.LifecycleExecutionRef,
        result.FailureContext?.ApplicationState ?? ExecutionApplicationState.NotApplied,
        result.Error?.Code.Value,
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<ProgramStepExecutionRecoveryResult> ToRecoveryAsync (
        ProgramStepExecutionRecovery recovery,
        PlayExitExecutionResult result,
        CancellationToken cancellationToken) => await ProjectLifecycleRecoveryAsync(
        recovery,
        result.IsSuccess ? result.Output!.LifecycleExecutionRef as ExecutionRef : result.FailureContext?.LifecycleExecutionRef,
        result.FailureContext?.ApplicationState ?? ExecutionApplicationState.NotApplied,
        result.Error?.Code.Value,
        cancellationToken).ConfigureAwait(false);

    private async ValueTask<ProgramStepExecutionPortResult> ProjectLifecycleStartAsync (
        ProgramStepExecutionStart start,
        ExecutionRef? actionReference,
        ExecutionApplicationState knownApplicationState,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        var terminal = await ResolveLifecycleTerminalAsync(start.Run, start.StepIndex, actionReference, knownApplicationState, errorCode, cancellationToken).ConfigureAwait(false);
        return terminal is null
            ? ProgramStepExecutionPortResult.CommunicationLost
            : ProgramStepExecutionPortResult.TerminallyReturned(terminal);
    }

    private async ValueTask<ProgramStepExecutionRecoveryResult> ProjectLifecycleRecoveryAsync (
        ProgramStepExecutionRecovery recovery,
        ExecutionRef? actionReference,
        ExecutionApplicationState knownApplicationState,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        var terminal = await ResolveLifecycleTerminalAsync(recovery.Run, recovery.StepIndex, actionReference, knownApplicationState, errorCode, cancellationToken).ConfigureAwait(false);
        return terminal is null
            ? ProgramStepExecutionRecoveryResult.CommunicationLost
            : ProgramStepExecutionRecoveryResult.TerminallyRecovered(terminal);
    }

    /// <summary>
    /// Lifecycle actions own both their terminal records and the meaning of their terminal reason.
    /// Program only accepts the action's reverified record for the exact reference it durably
    /// captured at start; an open reference deliberately remains recoverable instead of becoming
    /// a fabricated Step terminal.
    /// </summary>
    private async ValueTask<ProgramStepExecutionRecoveredTerminal?> ResolveLifecycleTerminalAsync (
        ProgramRunRecord run,
        int stepIndex,
        ExecutionRef? actionReference,
        ExecutionApplicationState knownApplicationState,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        // The Lifecycle start observer CASes the durable reference while the
        // action service is still executing. A terminal action result can
        // therefore return to this port with the pre-CAS `run` instance. Read
        // the active Step again; comparing against that stale instance would
        // reject the same kind/id/definition digest solely because its
        // LifecycleExecutionRef was not present yet.
        var persistedRun = await storeFactory.ForProject(hostContext.Project.UnityProject)
            .ReadAsync(run.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (persistedRun is null || persistedRun.Project != run.Project || persistedRun.Host != run.Host)
        {
            return Interrupted("PROGRAM_LIFECYCLE_STEP_UNAVAILABLE");
        }
        var step = GetStep(persistedRun, stepIndex);
        if (step.Execution != GetStep(run, stepIndex).Execution)
        {
            return Interrupted("PROGRAM_TERMINAL_PROJECTION_MISMATCH");
        }
        if (actionReference is null)
        {
            return Failed(knownApplicationState, errorCode ?? "PROGRAM_LIFECYCLE_FAILED");
        }
        if (!HasSameLifecycleIdentity(step.LifecycleExecutionRef, actionReference))
        {
            return Interrupted("PROGRAM_TERMINAL_PROJECTION_MISMATCH");
        }
        if (actionReference.Lifecycle != ExecutionLifecycle.Terminal)
        {
            return null;
        }

        var resolution = await lifecycleReconnectResolver.ResolveAsync(
                hostContext.Project.UnityProject,
                new LifecycleExecutionDefinition(GetLifecycleKind(step.Command)),
                actionReference,
                cancellationToken)
            .ConfigureAwait(false);
        if (resolution is not LifecycleExecutionReconnectResolution.Terminal resolved
            || !HasSameLifecycleIdentity(actionReference, resolved.ExecutionReference)
            || !HasExpectedTerminalRecord(step.Command, resolved.TerminalRecord)
            || resolved.TerminalRecord.Project != persistedRun.Project
            // The reconnect resolver re-reads the #499 Lifecycle start and terminal
            // records and verifies the accepted current endpoint generation.  Program
            // owns the fixed host boundary only: a domain reload may legitimately
            // advance CurrentEndpointRegistrationGenerationId after action start.
            || !ProgramRunRecord.HasSameProgramFixedHost(
                persistedRun.Host,
                resolved.TerminalRecord.Host))
        {
            return Interrupted("PROGRAM_TERMINAL_RECORD_INVALID");
        }

        var record = resolved.TerminalRecord;
        return record.TerminalReason == LifecycleExecutionTerminalReason.Completed
            ? Completed(record.Verdict, record.ApplicationState, record.TerminalGeneration, resolved.ExecutionReference)
            : Failed(record.ApplicationState, errorCode ?? GetTerminalReasonCode(record.TerminalReason), record.TerminalGeneration, resolved.ExecutionReference);
    }

    private static bool HasSameLifecycleIdentity (ExecutionRef? expected, ExecutionRef actual) =>
        expected is not null
        && expected.Kind == actual.Kind
        && expected.Id == actual.Id
        && expected.DefinitionDigest == actual.DefinitionDigest;

    internal static LifecycleExecutionKind GetLifecycleKind (string command) => command switch
    {
        "refresh" => LifecycleExecutionKind.Refresh,
        "compile" => LifecycleExecutionKind.Compile,
        "play.enter" => LifecycleExecutionKind.PlayEnter,
        "play.exit" => LifecycleExecutionKind.PlayExit,
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };

    private static bool HasExpectedTerminalRecord (string command, LifecycleExecutionTerminalRecord terminalRecord) => command switch
    {
        "refresh" => terminalRecord is RefreshLifecycleExecutionTerminalRecord,
        "compile" => terminalRecord is CompileLifecycleExecutionTerminalRecord,
        "play.enter" => terminalRecord is PlayEnterLifecycleExecutionTerminalRecord,
        "play.exit" => terminalRecord is PlayExitLifecycleExecutionTerminalRecord,
        _ => false,
    };

    private static string GetTerminalReasonCode (LifecycleExecutionTerminalReason reason) => reason switch
    {
        LifecycleExecutionTerminalReason.ActionFailed => "LIFECYCLE_EXECUTION_ACTION_FAILED",
        LifecycleExecutionTerminalReason.DeadlineExceeded => "LIFECYCLE_EXECUTION_DEADLINE_EXCEEDED",
        LifecycleExecutionTerminalReason.ProjectMismatch => "LIFECYCLE_EXECUTION_PROJECT_MISMATCH",
        LifecycleExecutionTerminalReason.HostMismatch => "LIFECYCLE_EXECUTION_HOST_MISMATCH",
        LifecycleExecutionTerminalReason.GenerationMismatch => "LIFECYCLE_EXECUTION_GENERATION_MISMATCH",
        LifecycleExecutionTerminalReason.UnityExited => "LIFECYCLE_EXECUTION_UNITY_EXITED",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    private static ProgramStepExecutionRecoveredTerminal Completed (
        Verdict? verdict,
        ExecutionApplicationState applicationState,
        UnityEditorGenerationSnapshot? generationAfter = null,
        ExecutionRef? lifecycleExecutionRef = null) =>
        new(ProgramStepState.Completed, verdict, applicationState, null, generationAfter, lifecycleExecutionRef);

    private static ProgramStepExecutionRecoveredTerminal Failed (
        ExecutionApplicationState applicationState,
        string errorCode = "PROGRAM_LIFECYCLE_FAILED",
        UnityEditorGenerationSnapshot? generationAfter = null,
        ExecutionRef? lifecycleExecutionRef = null) =>
        new(ProgramStepState.Failed, null, applicationState, errorCode, generationAfter, lifecycleExecutionRef);

    private static ProgramStepExecutionRecoveredTerminal LocalPreflightFailed (string errorCode) =>
        new(
            ProgramStepState.Failed,
            null,
            ExecutionApplicationState.NotApplied,
            errorCode,
            Origin: ProgramStepExecutionTerminalOrigin.LocalPreflight);

    private static ProgramStepExecutionRecoveredTerminal Interrupted (string errorCode) =>
        new(ProgramStepState.Interrupted, null, ExecutionApplicationState.Indeterminate, errorCode);

    private ExecutionDeadline CreateDeadline (DateTimeOffset utcDeadline)
    {
        if (ExecutionDeadline.TryStartUntil(utcDeadline, timeProvider, out var deadline))
        {
            return deadline!;
        }
        return ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1), timeProvider);
    }

    private static ExecutionMode ResolveMode (ProgramRunRecord run) => run.FixedContext.ExecutionMode.RequestedMode switch
    {
        "auto" => ExecutionMode.Auto,
        "daemon" => ExecutionMode.Daemon,
        "oneshot" => ExecutionMode.Oneshot,
        _ => throw new InvalidOperationException("Program Run records an unsupported execution mode."),
    };

    private static ProgramRunStepRecord GetStep (ProgramRunRecord run, int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= run.Steps.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(stepIndex));
        }
        return run.Steps[stepIndex];
    }

    /// <summary> The closed Program projection of a synchronous screenshot outcome. </summary>
    private sealed record ProgramScreenshotStepResult (
        ProjectIdentityInfo? Project,
        ProgramScreenshotCapture? Capture,
        ArtifactRef? Artifact,
        ProgramScreenshotFailure? Failure)
    {
        public static ProgramScreenshotStepResult Success (ScreenshotCaptureOutput output)
        {
            ArgumentNullException.ThrowIfNull(output);
            var capture = output.Capture;
            return new ProgramScreenshotStepResult(
                output.Project,
                new ProgramScreenshotCapture(
                    capture.Target,
                    capture.SizeMode,
                    capture.RequestedDimensions,
                    capture.Dimensions,
                    capture.ProjectColorSpace,
                    capture.State.LifecycleState,
                    capture.State.CompileState,
                    capture.State.Generations,
                    capture.State.PlayMode.State),
                output.Artifact,
                null);
        }

        public static ProgramScreenshotStepResult FromFailure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new ProgramScreenshotStepResult(
                null,
                null,
                null,
                new ProgramScreenshotFailure(error.Code?.Value ?? "PROGRAM_SCREENSHOT_FAILED", error.Message));
        }
    }

    private sealed record ProgramScreenshotCapture (
        IpcScreenshotTarget Target,
        IpcScreenshotSizeMode SizeMode,
        PixelDimensions? RequestedDimensions,
        PixelDimensions Dimensions,
        UnityProjectColorSpace ProjectColorSpace,
        UnityEditorLifecycleState LifecycleStateAtCapture,
        UnityEditorCompileState CompileStateAtCapture,
        UnityEditorGenerationSnapshot Generations,
        UnityEditorPlayModeState PlayModeState);

    private sealed record ProgramScreenshotFailure (string ErrorCode, string Message);
}

/// <summary>CASes the provider-confirmed Lifecycle Start Record into the active Program Step.</summary>
internal sealed class ProgramLifecycleStartObserver : ILifecycleExecutionStartObserver
{
    private readonly IProgramRunStoreFactory storeFactory;
    private readonly ResolvedUnityProjectContext project;
    private readonly ProgramStepExecutionStart start;

    public ProgramLifecycleStartObserver (
        IProgramRunStoreFactory storeFactory,
        ResolvedUnityProjectContext project,
        ProgramStepExecutionStart start)
    {
        this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
        this.project = project ?? throw new ArgumentNullException(nameof(project));
        this.start = start ?? throw new ArgumentNullException(nameof(start));
    }

    public async ValueTask<LifecycleExecutionStartObservation> ObserveAsync (LifecycleExecutionStartBinding lifecycleStart)
    {
        ArgumentNullException.ThrowIfNull(lifecycleStart);
        var store = storeFactory.ForProject(project);
        while (true)
        {
            var current = await store.ReadAsync(start.Run.RunId, CancellationToken.None).ConfigureAwait(false);
            if (current is null || current.Cursor != start.StepIndex || current.Steps[start.StepIndex].Execution != start.Execution)
            {
                return new LifecycleExecutionStartObservation.Rejected(ApplicationFailure.InternalError("Program Step changed before Lifecycle start could be recorded."));
            }
            var step = current.Steps[start.StepIndex];
            if (!ProgramRunStateSemantics.IsOngoing(step.State))
            {
                return new LifecycleExecutionStartObservation.Rejected(ApplicationFailure.InternalError(
                    "Program Step became terminal before Lifecycle start could be recorded."));
            }
            var expectedDefinition = new LifecycleExecutionDefinition(ProgramLifecycleStepExecutionPort.GetLifecycleKind(step.Command));
            if (lifecycleStart.LifecycleExecutionRef.Kind.Value != expectedDefinition.ExecutionKind.Value
                || lifecycleStart.LifecycleExecutionRef.DefinitionDigest != LifecycleExecutionDefinitionDigest.Calculate(expectedDefinition)
                || lifecycleStart.Project != current.Project
                || !ProgramRunRecord.HasSameProgramFixedHost(
                    current.Host,
                    lifecycleStart.Host)
                || lifecycleStart.StartedGeneration != (current.CurrentEditorGeneration ?? current.StartedGeneration)
                || lifecycleStart.DeadlineUtc != start.Execution.DeadlineUtc)
            {
                return new LifecycleExecutionStartObservation.Rejected(ApplicationFailure.ContractViolation(
                    "Lifecycle start facts do not match the Program Run's fixed action, project, host, generation, or deadline."));
            }
            if (step.LifecycleExecutionRef is not null)
            {
                return step.LifecycleExecutionRef == lifecycleStart.LifecycleExecutionRef
                    ? LifecycleExecutionStartObservation.Observed.Instance
                    : new LifecycleExecutionStartObservation.Rejected(ApplicationFailure.InternalError("Program Step already records another Lifecycle Execution."));
            }
            var steps = current.Steps.Select((item, index) => index == start.StepIndex
                ? item with
                {
                    // A durable Lifecycle Start Record is the Step's start
                    // boundary. Persist its reference, fixed generation, and
                    // planning-to-running transition atomically so a typed
                    // terminal can only follow the normal state machine.
                    State = ProgramStepState.Running,
                    LifecycleExecutionRef = lifecycleStart.LifecycleExecutionRef,
                    GenerationBefore = current.CurrentEditorGeneration
                        ?? current.StartedGeneration,
                }
                : item).ToArray();
            var replacement = new ProgramRunRecord(current.SchemaVersion, current.Version + 1, current.RunId, current.DefinitionDigest,
                current.DefinitionSnapshotRef, current.Project, current.FixedContext, current.Host, current.StartedGeneration,
                current.CurrentEditorGeneration, current.DeadlineUtc, current.StartedAtUtc, current.UpdatedAtUtc, current.State,
                current.Cursor, steps, current.ChildExecutionRefs, current.Cancellation, current.TerminalRecordRef)
            {
                SupervisorObservation = current.SupervisorObservation,
                HostObservation = current.HostObservation,
                TerminalReasonCode = current.TerminalReasonCode,
            };
            var exchange = await store.CompareExchangeAsync(current, replacement, CancellationToken.None).ConfigureAwait(false);
            if (exchange.Exchanged)
            {
                return LifecycleExecutionStartObservation.Observed.Instance;
            }
        }
    }
}
