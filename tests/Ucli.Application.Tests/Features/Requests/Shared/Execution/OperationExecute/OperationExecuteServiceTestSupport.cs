using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.OperationExecute;

internal static class OperationExecuteServiceTestSupport
{
    public static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    private static readonly JsonElement EmptyArgs = JsonSerializer.SerializeToElement(new { });

    public static readonly UcliOperationDescriptor RefreshDescriptor = new(
        Name: UcliPrimitiveOperationNames.ProjectRefresh,
        Kind: UcliOperationKind.Command,
        Policy: OperationPolicy.Advanced,
        ArgsSchemaJson: """{"type":"object","additionalProperties":false}""",
        DescriptorDigest: Sha256Digest.Parse(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
        VerdictContract: null,
        ResultSchemaJson: null,
        Exposure: UcliOperationExposure.Public);

    public static readonly OperationExecuteDefinition RefreshOperation = new(
        command: UcliCommandIds.Refresh,
        operationId: new IpcExecuteStepId("refresh"),
        operationName: UcliPrimitiveOperationNames.ProjectRefresh,
        args: EmptyArgs,
        successMessage: "uCLI refresh completed.");

    public static OperationExecuteService CreateService (
        StaticProjectContextResolver projectContextResolver,
        RecordingOperationAuthorizationService authorizationService,
        IUnityRequestExecutor unityRequestExecutor,
        IOperationCatalog operationCatalog,
        TestMutationReadPostconditionStore? readPostconditionStore = null,
        TimeProvider? timeProvider = null)
    {
        return new OperationExecuteService(
            projectContextResolver,
            operationCatalog,
            authorizationService,
            unityRequestExecutor,
            readPostconditionStore ?? new TestMutationReadPostconditionStore(),
            timeProvider ?? TimeProvider.System);
    }

    public static RecordingOperationCatalog CreateRefreshOperationCatalog ()
    {
        return new RecordingOperationCatalog
        {
            Operations =
            [
                RefreshDescriptor,
            ],
        };
    }

    public static StaticProjectContextResolver CreateProjectContextResolver (UcliConfig? config = null)
    {
        return new StaticProjectContextResolver(ProjectContextResolutionResult.Success(
            ProjectContextTestFactory.CreateRepositoryFixtureProject(config)));
    }

    public static RecordingOperationAuthorizationService CreateAllowedAuthorizationService ()
    {
        return new RecordingOperationAuthorizationService(OperationAuthorizationResult.Allowed());
    }

    public static OperationExecuteInput CreateInput (
        UnityExecutionMode? mode,
        int? timeoutMilliseconds,
        bool failFast,
        string? projectPath = "/repo/UnityProject")
    {
        return new OperationExecuteInput(
            ProjectPath: projectPath,
            Mode: mode,
            TimeoutMilliseconds: timeoutMilliseconds,
            FailFast: failFast);
    }

    public static UnityRequestExecutionResult CreatePlanSuccessResult (string planToken)
    {
        return UnityRequestExecutionResult.Success(ExecuteUnityRequestResponseTestFactory.Create(
            status: IpcResponseStatus.Ok,
            opResults:
            [
                CreatePlanOperationResult(),
            ],
            errors: [],
            planToken: planToken));
    }

    public static UnityRequestExecutionResult CreateCallSuccessResult (
        JsonElement? result = null,
        IReadOnlyList<IpcExecuteTouchedResource>? touched = null,
        IpcExecuteReadPostcondition? readPostcondition = null,
        bool changed = true)
    {
        return UnityRequestExecutionResult.Success(ExecuteUnityRequestResponseTestFactory.Create(
            status: IpcResponseStatus.Ok,
            opResults:
            [
                CreateCallOperationResult(result, touched, changed),
            ],
            errors: [],
            readPostcondition: readPostcondition));
    }

    public static IpcExecuteOperationResult CreatePlanOperationResult ()
    {
        return new IpcExecuteOperationResult(
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: IpcExecuteOperationPhase.Plan,
            Applied: false,
            Changed: false,
            Touched: [],
            OperationDescriptorDigest: RefreshDescriptor.DescriptorDigest,
            Verdict: null,
            Result: null,
            Diagnostics: []);
    }

    public static IpcExecuteOperationResult CreateCallOperationResult (
        JsonElement? result = null,
        IReadOnlyList<IpcExecuteTouchedResource>? touched = null,
        bool changed = true)
    {
        return new IpcExecuteOperationResult(
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: IpcExecuteOperationPhase.Call,
            Applied: true,
            Changed: changed,
            Touched: touched ?? [],
            OperationDescriptorDigest: RefreshDescriptor.DescriptorDigest,
            Verdict: null,
            Result: result,
            Diagnostics: []);
    }
}
