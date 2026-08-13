using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Conversion;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Validation;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary>
/// Produces the fixed Request facts that a Program call must retain before it
/// can admit the Call boundary. It uses only the Program's selected host.
/// </summary>
internal sealed class ProgramCallPreflightService
{
    private readonly IProgramFixedHostCatalogReader catalogReader;
    private readonly IRequestStaticValidator requestStaticValidator;
    private readonly IValidateRequestJsonParser requestParser;
    private readonly IProgramArtifactStoreFactory artifactStoreFactory;

    public ProgramCallPreflightService (
        IProgramFixedHostCatalogReader catalogReader,
        IRequestStaticValidator requestStaticValidator,
        IValidateRequestJsonParser requestParser,
        IProgramArtifactStoreFactory artifactStoreFactory)
    {
        this.catalogReader = catalogReader ?? throw new ArgumentNullException(nameof(catalogReader));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
        this.requestParser = requestParser ?? throw new ArgumentNullException(nameof(requestParser));
        this.artifactStoreFactory = artifactStoreFactory ?? throw new ArgumentNullException(nameof(artifactStoreFactory));
    }

    public async ValueTask<ProgramCallPreflightResult> PrepareAsync (
        ProgramRunRecord run,
        ProjectContext project,
        IUnityExecutionHostBinding binding,
        JsonElement requestDocument,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(deadline);
        var requestJson = JsonSerializer.Serialize(requestDocument, IpcJsonSerializerOptions.Default);
        var parsed = requestParser.Parse(requestJson);
        if (!parsed.IsSuccess)
        {
            return ProgramCallPreflightResult.Refused("PROGRAM_CALL_REQUEST_INVALID");
        }

        var request = parsed.Request! with { AllowPlayMode = run.FixedContext.Authorization.AllowPlayMode };
        var executionContext = await ObserveExecutionContextAsync(run, binding, deadline, cancellationToken).ConfigureAwait(false);
        if (executionContext is null)
        {
            return ProgramCallPreflightResult.Refused("PROGRAM_CALL_GENERATION_UNAVAILABLE");
        }

        var catalog = await catalogReader.ReadAsync(
                project,
                binding,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalog.IsSuccess)
        {
            return ProgramCallPreflightResult.Refused("PROGRAM_CALL_CATALOG_UNAVAILABLE");
        }

        var validationCatalog = RequestStaticValidationCatalog.Available(catalog.Descriptors!);
        var validation = await requestStaticValidator.ValidateAsync(request, validationCatalog, project.Config, cancellationToken).ConfigureAwait(false);
        if (validation.Error is not null || !validation.IsValid)
        {
            return ProgramCallPreflightResult.Refused("PROGRAM_CALL_STATIC_PREFLIGHT_REJECTED");
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return ProgramCallPreflightResult.Refused("PROGRAM_CALL_PREFLIGHT_TIMEOUT");
        }

        var planExecution = await binding.ExecuteAsync(
                UcliCommandIds.Plan,
                new UnityRequestPayload.ExecuteJson(UcliCommandIds.Plan, requestDocument.Clone(), FailFast: run.FixedContext.FailFast, AllowPlayMode: run.FixedContext.Authorization.AllowPlayMode),
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!planExecution.IsSuccess || planExecution.Response!.Errors.Count != 0)
        {
            return ProgramCallPreflightResult.Refused("PROGRAM_CALL_PLAN_REJECTED");
        }
        var converted = ExecuteResponseConverter.Convert(planExecution.Response, binding.Project);
        if (!OperationExecutionResultContractValidator.TryValidate(request, validationCatalog.OperationsByName, IpcExecuteOperationPhase.Plan, converted, out _)
            || !converted.IsSuccess || converted.PlanToken is null)
        {
            return ProgramCallPreflightResult.Refused("PROGRAM_CALL_PLAN_CONTRACT_INVALID");
        }

        var store = artifactStoreFactory.ForProject(binding.Project);
        var planBytes = JsonSerializer.SerializeToUtf8Bytes(planExecution.Response.Payload, IpcJsonSerializerOptions.Default);
        var planRef = await store.PublishAsync(run.RunId, ProgramTerminalArtifactContract.RequestPlanKind, ProgramTerminalArtifactContract.JsonMediaType, planBytes, cancellationToken).ConfigureAwait(false);
        var descriptors = new List<ProgramCallDescriptor>(request.Steps.Count);
        foreach (var step in request.Steps)
        {
            if (step.Kind != IpcExecuteStepKind.Op)
            {
                continue;
            }

            var descriptor = validationCatalog.OperationsByName[step.Op!];
            var bytes = JsonSerializer.SerializeToUtf8Bytes(descriptor, IpcJsonSerializerOptions.Default);
            var artifact = await store.PublishAsync(run.RunId, ProgramTerminalArtifactContract.OperationDescriptorKind, ProgramTerminalArtifactContract.JsonMediaType, bytes, cancellationToken).ConfigureAwait(false);
            descriptors.Add(new ProgramCallDescriptor(artifact, descriptor.DescriptorDigest));
        }

        var callRequest = new IpcExecuteRequest("call", requestDocument.Clone())
        {
            AllowDangerous = run.FixedContext.Authorization.AllowDangerous,
            AllowPlayMode = run.FixedContext.Authorization.AllowPlayMode,
            PlanToken = converted.PlanToken,
        };
        return ProgramCallPreflightResult.Success(new ProgramCallPreflight(
            callRequest,
            Sha256Digest.Compute(JsonSerializer.SerializeToUtf8Bytes(requestDocument, IpcJsonSerializerOptions.Default)),
            Sha256Digest.Compute(System.Text.Encoding.UTF8.GetBytes(converted.PlanToken)),
            executionContext.Host,
            executionContext.Generation,
            planRef,
            descriptors));
    }

    private static async ValueTask<ProgramCallExecutionContext?> ObserveExecutionContextAsync (ProgramRunRecord run, IUnityExecutionHostBinding binding, ExecutionDeadline deadline, CancellationToken cancellationToken)
    {
        var authorization = run.FixedContext.Authorization;
        var effectiveAuthorization = new IpcProgramEffectiveAuthorizationSnapshot(
            authorization.AllowDangerous,
            authorization.AllowPlayMode,
            Sha256Digest.Parse(authorization.Digest));
        var execution = await binding.ExecuteAsync(
                UcliCommandIds.ProgramRun,
                new UnityRequestPayload.ProgramExecutionContext(effectiveAuthorization),
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!execution.IsSuccess || execution.Response!.Errors.Count != 0
            || !IpcPayloadCodec.TryDeserialize(execution.Response.Payload, out IpcProgramExecutionContextResponse context, out _)
            || !ProgramRunRecord.HasSameProgramFixedHost(run.Host, context.Host))
        {
            return null;
        }
        return new ProgramCallExecutionContext(context.Host, context.Generation);
    }

}

internal sealed record ProgramCallPreflight (
    IpcExecuteRequest Request,
    Sha256Digest RequestDigest,
    Sha256Digest PlanTokenDigest,
    LifecycleExecutionHostRegistration Host,
    UnityEditorGenerationSnapshot Generation,
    ArtifactRef RequestPlanRef,
    IReadOnlyList<ProgramCallDescriptor> Descriptors);

internal sealed record ProgramCallDescriptor (ArtifactRef Artifact, Sha256Digest Digest);

internal sealed record ProgramCallExecutionContext (
    LifecycleExecutionHostRegistration Host,
    UnityEditorGenerationSnapshot Generation);

internal sealed record ProgramCallPreflightResult (ProgramCallPreflight? Preflight, string? ErrorCode)
{
    public bool IsSuccess => Preflight is not null;
    public static ProgramCallPreflightResult Success (ProgramCallPreflight preflight) => new(preflight ?? throw new ArgumentNullException(nameof(preflight)), null);
    public static ProgramCallPreflightResult Refused (string errorCode) => new(null, errorCode);
}
