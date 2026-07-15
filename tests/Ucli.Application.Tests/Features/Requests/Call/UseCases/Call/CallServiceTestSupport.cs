using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Tests;

internal static class CallServiceTestSupport
{
    public static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    public static PhaseExecutionPreparedRequest CreatePreparedRequest (
        string requestJson,
        ValidateRequest request,
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName,
        UcliConfig? config = null)
    {
        return new PhaseExecutionPreparedRequest(
            PreparedRequest: new PreparedRequestContext(
                requestJson: requestJson,
                request: request,
                projectContext: ProjectContextTestFactory.CreateRepositoryFixtureProject(config)),
            OperationsByName: operationsByName);
    }

    public static PhaseExecutionPreparedRequest CreateSingleOperationPreparedRequest (
        string operationName,
        OperationPolicy policy,
        UcliConfig? config = null)
    {
        return CreatePreparedRequest(
            requestJson: CreateOpRequestJson(operationName),
            request: CreateOpRequest(operationName),
            operationsByName: CreateOperationsByName(CreateOperationDescriptor(operationName, policy)),
            config);
    }

    public static CallService CreateService (
        PhaseExecutionPreflightResult preflightResult,
        IUnityRequestExecutor ipcRequestExecutor,
        TimeProvider? timeProvider = null,
        RequestPreparationResult? requestPreparationResult = null,
        RecordingPhaseExecutionPreflightService? preflightService = null,
        TestMutationReadPostconditionStore? mutationReadPostconditionStore = null)
    {
        ArgumentNullException.ThrowIfNull(preflightResult);
        ArgumentNullException.ThrowIfNull(ipcRequestExecutor);

        requestPreparationResult ??= preflightResult.PreparedRequest != null
            ? RequestPreparationResult.Success(preflightResult.PreparedRequest.PreparedRequest)
            : throw new InvalidOperationException("A prepared request is required when request preparation is not explicitly configured.");

        return new CallService(
            new RecordingRequestPreparationService
            {
                PrepareResult = requestPreparationResult,
            },
            preflightService ?? new RecordingPhaseExecutionPreflightService
            {
                Result = preflightResult,
            },
            new CallDangerousOperationGuard(),
            new CallUnityExecutionService(ipcRequestExecutor, mutationReadPostconditionStore ?? new TestMutationReadPostconditionStore()),
            timeProvider ?? TimeProvider.System);
    }

    public static IReadOnlyDictionary<string, UcliOperationDescriptor> CreateOperationsByName (params UcliOperationDescriptor[] operations)
    {
        var operationsByName = new Dictionary<string, UcliOperationDescriptor>(operations.Length, StringComparer.Ordinal);
        for (var i = 0; i < operations.Length; i++)
        {
            operationsByName[operations[i].Name] = operations[i];
        }

        return operationsByName;
    }

    public static UcliOperationDescriptor CreateOperationDescriptor (
        string name,
        OperationPolicy policy)
    {
        return new UcliOperationDescriptor(
            Name: name,
            Kind: UcliOperationKind.Mutation,
            Policy: policy,
            ArgsSchemaJson: """{"type":"object","additionalProperties":false}""");
    }

    public static ValidateRequest CreateOpRequest (string operationName)
    {
        return new ValidateRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Steps:
            [
                new ValidateRequestStep(
                    Kind: IpcExecuteStepKind.Op,
                    StepId: new IpcExecuteStepId("step-1"),
                    Op: operationName,
                    Element: JsonSerializer.SerializeToElement(new
                    {
                        kind = "op",
                        id = "step-1",
                        op = operationName,
                        args = new { },
                    })),
            ]);
    }

    public static string CreateOpRequestJson (string operationName)
    {
        return JsonSerializer.Serialize(new
        {
            protocolVersion = IpcProtocol.CurrentVersion,
            steps = new[]
            {
                new
                {
                    kind = "op",
                    id = "step-1",
                    op = operationName,
                    args = new { },
                },
            },
        });
    }

    public static ValidateRequest CreateEditRequest ()
    {
        using var document = JsonDocument.Parse(CreateEditRequestJson());

        return new ValidateRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Steps:
            [
                new ValidateRequestStep(
                    Kind: IpcExecuteStepKind.Edit,
                    StepId: new IpcExecuteStepId("edit-1"),
                    Op: null,
                    Element: document.RootElement.GetProperty("steps")[0].Clone()),
            ]);
    }

    public static string CreateEditRequestJson ()
    {
        return """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "id": "edit-1",
                  "on": {
                    "scene": "Assets/Scenes/Main.unity"
                  },
                  "select": {
                    "gameObject": "Root/Spawner",
                    "cardinality": "one"
                  },
                  "actions": [
                    {
                      "kind": "ensureComponent",
                      "type": "UnityEngine.BoxCollider, UnityEngine.PhysicsModule",
                      "as": "collider"
                    },
                    {
                      "kind": "set",
                      "target": "$collider",
                      "values": {
                        "isTrigger": true
                      }
                    }
                  ],
                  "commit": "context"
                }
              ]
            }
            """;
    }
}
