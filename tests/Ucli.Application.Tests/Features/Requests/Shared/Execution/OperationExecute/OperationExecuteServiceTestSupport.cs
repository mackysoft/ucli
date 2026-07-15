using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.OperationExecute;

internal static class OperationExecuteServiceTestSupport
{
    public static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    private static readonly JsonElement EmptyArgs = JsonSerializer.SerializeToElement(new { });

    public static readonly OperationExecuteDefinition RefreshOperation = new(
        Command: UcliCommandIds.Refresh,
        OperationId: new IpcExecuteStepId("refresh"),
        Descriptor: new UcliOperationDescriptor(
            Name: UcliPrimitiveOperationNames.ProjectRefresh,
            Kind: UcliOperationKind.Command,
            Policy: OperationPolicy.Advanced,
            ArgsSchemaJson: """{"type":"object","additionalProperties":false}"""),
        Args: EmptyArgs,
        SuccessMessage: "uCLI refresh completed.",
        FailureMessage: "uCLI refresh failed.");

    public static OperationExecuteService CreateService (
        StaticProjectContextResolver projectContextResolver,
        RecordingOperationAuthorizationService authorizationService,
        IUnityRequestExecutor unityRequestExecutor,
        TestMutationReadPostconditionStore? readPostconditionStore = null,
        TimeProvider? timeProvider = null)
    {
        return new OperationExecuteService(
            projectContextResolver,
            authorizationService,
            unityRequestExecutor,
            readPostconditionStore ?? new TestMutationReadPostconditionStore(),
            timeProvider ?? TimeProvider.System);
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
        OperationExecutionReadPostcondition? readPostcondition = null,
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
            OpId: new IpcExecuteStepId("refresh"),
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: IpcExecuteOperationPhase.Plan,
            Applied: false,
            Changed: false,
            Touched: []);
    }

    public static IpcExecuteOperationResult CreateCallOperationResult (
        JsonElement? result = null,
        IReadOnlyList<IpcExecuteTouchedResource>? touched = null,
        bool changed = true)
    {
        return new IpcExecuteOperationResult(
            OpId: new IpcExecuteStepId("refresh"),
            Op: UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: IpcExecuteOperationPhase.Call,
            Applied: true,
            Changed: changed,
            Touched: touched ?? [])
        {
            Result = result,
        };
    }
}
