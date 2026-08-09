using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Tests;

internal static class PlanServiceTestSupport
{
    public static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    public static readonly Sha256Digest OperationDescriptorDigest =
        Sha256Digest.Compute("plan-service-operation-descriptor"u8);

    public const string OpRequestJson =
        """{"protocolVersion":1,"steps":[{"kind":"op","id":"describe","op":"ucli.go.describe","args":{}}]}""";

    public static PlanService CreateService (
        IRequestPreparationService? requestPreparationService = null,
        IRequestStaticValidationPreflightService? staticPreflightService = null,
        IOperationCatalog? operationCatalog = null,
        IRequestStaticValidator? requestStaticValidator = null,
        IUnityRequestExecutor? unityRequestExecutor = null,
        TimeProvider? timeProvider = null)
    {
        return new PlanService(
            requestPreparationService ?? CreateSuccessfulRequestPreparationService(),
            staticPreflightService ?? CreateSuccessfulPreflightService(),
            operationCatalog ?? new RecordingOperationCatalog
            {
                Operations = [],
            },
            requestStaticValidator ?? new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            },
            unityRequestExecutor ?? new RecordingUnityRequestExecutor(CreatePlanSuccess("plan-token-1")),
            timeProvider ?? TimeProvider.System);
    }

    public static PlanCommandInput CreateInput (
        UnityExecutionMode? mode = null,
        int? timeoutMilliseconds = null,
        ReadIndexMode? readIndexMode = null,
        bool failFast = false,
        bool allowPlayMode = false)
    {
        return new PlanCommandInput(
            ProjectPath: "/repo/UnityProject",
            Mode: mode,
            TimeoutMilliseconds: timeoutMilliseconds,
            ReadIndexMode: readIndexMode,
            FailFast: failFast,
            RequestJson: """{"steps":[]}""")
        {
            AllowPlayMode = allowPlayMode,
        };
    }

    public static UnityRequestExecutionResult CreatePlanSuccess (
        string? planToken,
        IReadOnlyList<IpcExecuteOperationResult>? opResults = null)
    {
        return UnityRequestExecutionResult.Success(
            ExecuteUnityRequestResponseTestFactory.Create(
                status: IpcResponseStatus.Ok,
                opResults: opResults ?? [],
                errors: [],
                planToken: planToken));
    }

    public static RequestStaticValidationPreflightResult CreateSuccessPreflightResult (
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex,
        RequestStaticValidationCatalog catalog)
    {
        return RequestStaticValidationPreflightResult.Success(
            preparedRequest,
            readIndex,
            catalog);
    }

    public static RecordingRequestStaticValidationPreflightService CreateSuccessfulPreflightService ()
    {
        return new RecordingRequestStaticValidationPreflightService
        {
            Result = CreateSuccessPreflightResult(
                CreatePreparedRequestContext(),
                CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    freshness: IndexFreshness.Probable,
                    fallbackReason: null),
                RequestStaticValidationCatalog.Available([])),
        };
    }

    public static RecordingRequestPreparationService CreateSuccessfulRequestPreparationService ()
    {
        return new RecordingRequestPreparationService
        {
            PrepareResult = RequestPreparationResult.Success(CreatePreparedRequestContext()),
        };
    }

    public static PlanService CreateOpService (
        UcliOperationDescriptor operationDescriptor,
        IUnityRequestExecutor unityRequestExecutor)
    {
        var preparedRequest = CreateOpPreparedRequestContext();
        return CreateService(
            requestPreparationService: new RecordingRequestPreparationService
            {
                PrepareResult = RequestPreparationResult.Success(preparedRequest),
            },
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = CreateSuccessPreflightResult(
                    preparedRequest,
                    CreateReadIndexInfo(
                        used: true,
                        hit: true,
                        freshness: IndexFreshness.Probable,
                        fallbackReason: null),
                    RequestStaticValidationCatalog.Available([operationDescriptor])),
            },
            unityRequestExecutor: unityRequestExecutor,
            timeProvider: new FakeTimeProvider(DateTimeOffset.UnixEpoch));
    }

    public static PreparedRequestContext CreatePreparedRequestContext ()
    {
        return new PreparedRequestContext(
            requestJson: """
                {
                  "protocolVersion": 1,
                  "steps": []
                }
                """,
            request: new ValidateRequest(
                ProtocolVersion: 1,
                Steps: Array.Empty<ValidateRequestStep>(),
                AllowPlayMode: false),
            projectContext: ProjectContextTestFactory.CreateRepositoryFixtureProject());
    }

    public static PreparedRequestContext CreateOpPreparedRequestContext ()
    {
        return new PreparedRequestContext(
            requestJson: OpRequestJson,
            request: new ValidateRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                Steps:
                [
                    new ValidateRequestStep(
                        Kind: IpcExecuteStepKind.Op,
                        StepIndex: 0,
                        Op: UcliPrimitiveOperationNames.GoDescribe,
                        Args: JsonSerializer.SerializeToElement(new
                        {
                        })),
                ],
                AllowPlayMode: false),
            projectContext: ProjectContextTestFactory.CreateRepositoryFixtureProject());
    }

    public static UcliOperationDescriptor CreateOperationDescriptor ()
    {
        return CreateOperationDescriptor(
            resultSchemaJson: null);
    }

    public static UcliOperationDescriptor CreateResultfulOperationDescriptor (
        string resultSchemaJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultSchemaJson);
        return CreateOperationDescriptor(resultSchemaJson);
    }

    private static UcliOperationDescriptor CreateOperationDescriptor (
        string? resultSchemaJson)
    {
        return new UcliOperationDescriptor(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: UcliOperationKind.Query,
            Policy: OperationPolicy.Safe,
            ArgsSchemaJson: """{"type":"object","additionalProperties":false}""",
            DescriptorDigest: OperationDescriptorDigest,
            VerdictContract: null,
            ResultSchemaJson: resultSchemaJson,
            Exposure: UcliOperationExposure.Public);
    }

    public static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        IndexFreshness freshness,
        string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoSource.Index,
            Freshness: freshness,
            GeneratedAtUtc: used
                ? DateTimeOffset.Parse("2026-03-06T00:00:00+00:00")
                : null,
            FallbackReason: fallbackReason);
    }
}
