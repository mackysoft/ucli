using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Tests;

internal static class CallServiceTestSupport
{
    public static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    public static readonly Sha256Digest OperationDescriptorDigest = Sha256Digest.Compute("call-service-operation-descriptor"u8);

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

        requestPreparationResult ??= RequestPreparationResult.Success(
            preflightResult.PreparedRequest.PreparedRequest);

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
            ArgsSchemaJson: """{"type":"object","additionalProperties":false}""",
            DescriptorDigest: OperationDescriptorDigest,
            VerdictContract: null,
            ResultSchemaJson: null,
            Exposure: UcliOperationExposure.Public);
    }

    public static UnityRequestResponse CreateUnityResponse (
        IpcResponseStatus status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken = null,
        IpcExecuteReadPostcondition? readPostcondition = null,
        IpcProjectIdentity? project = null)
    {
        ArgumentNullException.ThrowIfNull(opResults);

        return ExecuteUnityRequestResponseTestFactory.Create(
            status,
            opResults,
            errors,
            planToken,
            readPostcondition,
            project);
    }

    public static ValidateRequest CreateOpRequest (string operationName)
    {
        return new ValidateRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Steps:
            [
                new ValidateRequestStep(
                    Kind: IpcExecuteStepKind.Op,
                    StepIndex: 0,
                    Op: operationName,
                    Args: JsonSerializer.SerializeToElement(new
                    {
                    })),
            ],
            AllowPlayMode: false);
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
                    op = operationName,
                    args = new { },
                },
            },
        });
    }

    public static ValidateRequest CreateEditRequest ()
    {
        var result = new ValidateRequestJsonParser().Parse(CreateEditRequestJson());
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Request!;
    }

    public static string CreateEditRequestJson ()
    {
        return """
            {
              "protocolVersion": 1,
              "steps": [
                {
                  "kind": "edit",
                  "on": {
                    "kind": "scene",
                    "path": "Assets/Scenes/Main.unity"
                  },
                  "select": {
                    "kind": "gameObject",
                    "path": "Root/Spawner",
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
