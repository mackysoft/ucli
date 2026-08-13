using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Features.Programs.Supervision;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Programs.Planning;

/// <summary>
/// Validates only the current Program planning frontier against its already
/// selected host. It intentionally does not create a Run, persist artifacts,
/// or retain a Request plan token.
/// </summary>
internal sealed class ProgramPlanPreflightService
{
    private readonly IReadyService readyService;
    private readonly IProgramFixedHostCatalogReader catalogReader;
    private readonly IRequestStaticValidator requestStaticValidator;

    public ProgramPlanPreflightService (
        IReadyService readyService,
        IProgramFixedHostCatalogReader catalogReader,
        IRequestStaticValidator requestStaticValidator)
    {
        this.readyService = readyService ?? throw new ArgumentNullException(nameof(readyService));
        this.catalogReader = catalogReader ?? throw new ArgumentNullException(nameof(catalogReader));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
    }

    public async ValueTask<ProgramPlanPreflightResult> ValidateAsync (
        ResolvedProgramDefinition definition,
        ProjectContext project,
        IUnityExecutionHostBinding binding,
        ExecutionDeadline deadline,
        bool allowPlayMode,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(deadline);

        var projection = ProgramPlanProjection.Create(definition.Program, startIndex: 0);
        var requiredOptions = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var projected in projection.Steps)
        {
            if (projected.State != ProgramPlanStepState.Current)
            {
                continue;
            }

            var step = definition.Program.Steps[projected.Index];
            var result = await ValidateStepAsync(
                    definition, projected.Index, step, project, binding, deadline, allowPlayMode, failFast, cancellationToken)
                .ConfigureAwait(false);
            if (result.Diagnostic is not null)
            {
                return ProgramPlanPreflightResult.Failure(result.Diagnostic);
            }
            requiredOptions[projected.Index] = result.RequiredRunOptions;
        }
        return ProgramPlanPreflightResult.Success(requiredOptions);
    }

    private async ValueTask<ProgramPlanStepPreflightResult> ValidateStepAsync (
        ResolvedProgramDefinition definition,
        int index,
        ProgramStep step,
        ProjectContext project,
        IUnityExecutionHostBinding binding,
        ExecutionDeadline deadline,
        bool allowPlayMode,
        bool failFast,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return ProgramPlanStepPreflightResult.Failed("PROGRAM_PLAN_TIMEOUT", index, "Program plan deadline elapsed before the current Step could be validated.");
        }

        if (step is ReadyProgramStep)
        {
            var ready = await readyService.ObserveOnFixedHostAsync(project, binding, deadline, cancellationToken).ConfigureAwait(false);
            return ready.IsReady
                ? ProgramPlanStepPreflightResult.Ready([])
                : ProgramPlanStepPreflightResult.Failed(
                    ready.Failure?.Code.Value ?? "PROGRAM_PLAN_READY_UNAVAILABLE",
                    index,
                    ready.Failure?.Message ?? "The fixed Program host is not ready.");
        }

        if (step is ScreenshotGameProgramStep or ScreenshotSceneProgramStep)
        {
            return binding.Target == UnityExecutionTarget.Daemon
                ? ProgramPlanStepPreflightResult.Ready([])
                : ProgramPlanStepPreflightResult.Failed(
                    "PROGRAM_PLAN_SCREENSHOT_REQUIRES_GUI",
                    index,
                    "Screenshot Program Steps require the fixed GUI Editor host.");
        }

        if (step is not CallProgramStep)
        {
            // Lifecycle entry contracts are reached only after a durable Run
            // start record exists. Their side-effecting Start operation is not
            // a planning operation; fixed-host selection above is their plan
            // admission condition.
            return ProgramPlanStepPreflightResult.Ready([]);
        }

        var request = ResolveRequest(definition, index, step);
        if (request is null)
        {
            return ProgramPlanStepPreflightResult.Failed(
                "PROGRAM_PLAN_CALL_REQUEST_UNAVAILABLE", index, "The resolved Program call request is unavailable.");
        }

        if (request.Steps.Any(static step => step.Kind == IpcExecuteStepKind.Op && string.Equals(step.Op, "ucli.cs.eval", StringComparison.Ordinal)))
        {
            return ProgramPlanStepPreflightResult.Failed("PROGRAM_CALL_EVAL_NOT_ALLOWED", index, "Program call Steps cannot execute dedicated C# evaluation.");
        }

        var catalog = await catalogReader.ReadAsync(project, binding, deadline, cancellationToken).ConfigureAwait(false);
        if (!catalog.IsSuccess)
        {
            return ProgramPlanStepPreflightResult.Failed(
                "PROGRAM_CALL_CATALOG_UNAVAILABLE", index, "The fixed Program host did not provide an operation catalog.");
        }
        var validationCatalog = RequestStaticValidationCatalog.Available(catalog.Descriptors!);
        var validatedRequest = request with { AllowPlayMode = allowPlayMode };
        var validation = await requestStaticValidator.ValidateAsync(validatedRequest, validationCatalog, project.Config, cancellationToken).ConfigureAwait(false);
        if (validation.Error is not null)
        {
            return ProgramPlanStepPreflightResult.Failed(
                validation.Error.Code?.Value ?? "PROGRAM_CALL_STATIC_PREFLIGHT_REJECTED",
                index,
                validation.Error.Message);
        }
        if (!validation.IsValid)
        {
            var error = validation.Errors[0];
            return ProgramPlanStepPreflightResult.Failed(error.Code.Value, index, error.Message);
        }

        var planDocument = CreateRequestDocument(validatedRequest);
        var plan = await binding.ExecuteAsync(
                UcliCommandIds.Plan,
                new UnityRequestPayload.ExecuteJson(UcliCommandIds.Plan, planDocument, FailFast: failFast, AllowPlayMode: allowPlayMode),
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!plan.IsSuccess || plan.Response!.Errors.Count != 0)
        {
            var message = plan.IsSuccess
                ? plan.Response!.Errors[0].Message
                : plan.FailureInfo!.Message;
            return ProgramPlanStepPreflightResult.Failed("PROGRAM_CALL_PLAN_REJECTED", index, message);
        }
        var converted = ExecuteResponseConverter.Convert(plan.Response, binding.Project);
        if (!OperationExecutionResultContractValidator.TryValidate(
                validatedRequest,
                validationCatalog.OperationsByName,
                IpcExecuteOperationPhase.Plan,
                converted,
                out _)
            || !converted.IsSuccess
            || converted.PlanToken is null)
        {
            return ProgramPlanStepPreflightResult.Failed(
                "PROGRAM_CALL_PLAN_CONTRACT_INVALID", index, "The fixed Program host returned an invalid Request Plan.");
        }

        return ProgramPlanStepPreflightResult.Ready(GetRequiredRunOptions(validatedRequest, validationCatalog));
    }

    private static ValidateRequest? ResolveRequest (ResolvedProgramDefinition definition, int index, ProgramStep step) => step switch
    {
        InlineCallProgramStep inline => inline.Request,
        ReferencedCallProgramStep => definition.Sources.SingleOrDefault(source => source.InstancePath == $"/steps/{index}/requestPath")?.Request,
        _ => null,
    };

    private static JsonElement CreateRequestDocument (ValidateRequest request)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { steps = request.Steps }, IpcJsonSerializerOptions.Default);
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<string> GetRequiredRunOptions (
        ValidateRequest request,
        RequestStaticValidationCatalog catalog)
    {
        var dangerous = request.Steps.Any(step => step.Kind == IpcExecuteStepKind.Op
            && step.Op is not null
            && catalog.OperationsByName.TryGetValue(step.Op, out var operation)
            && operation.Policy == OperationPolicy.Dangerous);
        return dangerous ? ["allowDangerous"] : [];
    }

}

internal sealed record ProgramPlanPreflightResult (
    IReadOnlyDictionary<int, IReadOnlyList<string>> RequiredRunOptions,
    ProgramDiagnostic? Diagnostic)
{
    public bool IsSuccess => Diagnostic is null;

    public static ProgramPlanPreflightResult Success (IReadOnlyDictionary<int, IReadOnlyList<string>> requiredRunOptions) =>
        new(requiredRunOptions, null);

    public static ProgramPlanPreflightResult Failure (ProgramDiagnostic diagnostic) =>
        new(new Dictionary<int, IReadOnlyList<string>>(), diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)));
}

internal sealed record ProgramPlanStepPreflightResult (
    IReadOnlyList<string> RequiredRunOptions,
    ProgramDiagnostic? Diagnostic)
{
    public static ProgramPlanStepPreflightResult Ready (IReadOnlyList<string> requiredRunOptions) =>
        new(requiredRunOptions, null);

    public static ProgramPlanStepPreflightResult Failed (string code, int index, string message) =>
        new([], new ProgramDiagnostic(code, $"/steps/{index}", message));
}
