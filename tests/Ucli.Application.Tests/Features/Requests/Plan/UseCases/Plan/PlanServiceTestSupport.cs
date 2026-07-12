using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class PlanServiceTestSupport
{
    public static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    public static readonly UcliCode[] UnityExecutionToolErrorCodes =
    [
        EditorLifecycleErrorCodes.EditorPlaymode,
        ExecutionErrorCodes.IpcTimeout,
    ];

    public static PlanService CreateService (
        IRequestPreparationService? requestPreparationService = null,
        IRequestStaticValidationPreflightService? staticPreflightService = null,
        IRequestStaticValidationService? staticValidationService = null,
        IUnityRequestExecutor? unityRequestExecutor = null)
    {
        return new PlanService(
            requestPreparationService ?? CreateSuccessfulRequestPreparationService(),
            staticPreflightService ?? CreateSuccessfulPreflightService(),
            staticValidationService ?? new RecordingRequestStaticValidationService
            {
                Result = ValidationResult.Success(),
            },
            unityRequestExecutor ?? new RecordingUnityRequestExecutor(CreatePlanSuccess("plan-token-1")));
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
                status: IpcProtocol.StatusOk,
                opResults: opResults ?? [],
                errors: [],
                planToken: planToken));
    }

    public static RequestStaticValidationPreflightResult CreateSuccessPreflightResult (ReadIndexInfo readIndex)
    {
        return RequestStaticValidationPreflightResult.Success(CreatePreparedRequestContext(), readIndex);
    }

    public static RecordingRequestStaticValidationPreflightService CreateSuccessfulPreflightService ()
    {
        return new RecordingRequestStaticValidationPreflightService
        {
            Result = CreateSuccessPreflightResult(CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: IndexFreshness.Probable,
                fallbackReason: null)),
        };
    }

    public static RecordingRequestPreparationService CreateSuccessfulRequestPreparationService ()
    {
        return new RecordingRequestPreparationService
        {
            PrepareResult = RequestPreparationResult.Success(CreatePreparedRequestContext()),
        };
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
                Steps: Array.Empty<ValidateRequestStep?>()),
            projectContext: ProjectContextTestFactory.CreateRepositoryFixtureProject());
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
