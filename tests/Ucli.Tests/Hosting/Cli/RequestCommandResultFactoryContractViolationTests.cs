using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests;

public sealed class RequestCommandResultFactoryContractViolationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    [Trait("Size", "Small")]
    public void Plan_Create_WhenContractViolationExists_EmitsErrorCodeAndPayloadDetails ()
    {
        var result = PlanCommandResultFactory.Create(PlanServiceResult.Failure(
            "Operation contract violation.",
            [CreateContractViolationFailure()],
            new PlanExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults: [CreateOpResult()],
                ReadIndex: CreateReadIndexInfo(),
                PlanToken: null)
            {
                ContractViolations = [CreateContractViolation()],
            }));

        AssertContractViolationPayload(result, UcliCommandNames.Plan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Call_Create_WhenContractViolationExists_EmitsErrorCodeAndPayloadDetails ()
    {
        var result = CallCommandResultFactory.Create(CallServiceResult.Failure(
            "Operation contract violation.",
            [CreateContractViolationFailure()],
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults: [CreateOpResult()],
                Plan: null,
                ReadPostcondition: null)
            {
                ContractViolations = [CreateContractViolation()],
            }));

        AssertContractViolationPayload(result, UcliCommandNames.Call);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Query_Create_WhenContractViolationExists_EmitsErrorCodeAndPayloadDetails ()
    {
        var result = QueryCommandResultFactory.Create(QueryServiceResultFactory.Failure(
            UcliCommandNames.QueryAssetsFind,
            "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            [CreateOpResult()],
            [CreateContractViolationFailure()],
            "Operation contract violation.",
            CreateReadIndexInfo(),
            ProjectIdentityInfoTestFactory.Create(),
            [CreateContractViolation()]));

        AssertContractViolationPayload(result, UcliCommandNames.QueryAssetsFind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_Create_WhenContractViolationExists_EmitsErrorCodeAndPayloadDetails ()
    {
        var result = ResolveCommandResultFactory.Create(ResolveServiceResultFactory.Failure(
            "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            [CreateOpResult()],
            [CreateContractViolationFailure()],
            CreateReadIndexInfo(),
            ProjectIdentityInfoTestFactory.Create(),
            [CreateContractViolation()]));

        AssertContractViolationPayload(result, UcliCommandNames.Resolve);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Refresh_Create_WhenContractViolationExists_EmitsErrorCodeAndPayloadDetails ()
    {
        var result = RefreshCommandResultFactory.Create(OperationExecuteResultFactory.Failure(
            "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            [CreateOpResult()],
            [CreateContractViolationFailure()],
            "Operation contract violation.",
            readPostcondition: null,
            project: ProjectIdentityInfoTestFactory.Create(),
            contractViolations: [CreateContractViolation()]));

        AssertContractViolationPayload(result, UcliCommandNames.Refresh);
    }

    private static void AssertContractViolationPayload (
        CommandResult result,
        string expectedCommand)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
        CommandResultAssert.HasStandardEnvelope(
            json.RootElement,
            expectedCommand,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        JsonAssert.For(json.RootElement)
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", ExecuteRequestErrorCodes.OperationContractViolation.Value)
                .HasString("message", "Operation contract violation.")
                .HasString("opId", "query-1"))
            .HasProperty("payload", payload => payload
                .HasArrayLength("opResults", 1)
                .HasArrayLength("contractViolations", 1)
                .HasProperty("contractViolations", 0, violation => violation
                    .HasString("opId", "query-1")
                    .HasString("operation", UcliPrimitiveOperationNames.AssetsFind)
                    .HasString("expectedFact", "operation.kind=query")
                    .HasString("observedResult", "opResults[].applied=true")
                    .HasString("applicationState", IpcExecuteApplicationStateNames.Applied)));
    }

    private static ApplicationFailure CreateContractViolationFailure ()
    {
        return ApplicationFailure.ContractViolation(
            "Operation contract violation.",
            ExecuteRequestErrorCodes.OperationContractViolation,
            "query-1");
    }

    private static OperationExecutionContractViolation CreateContractViolation ()
    {
        return new OperationExecutionContractViolation(
            OpId: "query-1",
            Operation: UcliPrimitiveOperationNames.AssetsFind,
            ExpectedFact: "operation.kind=query",
            ObservedResult: "opResults[].applied=true",
            ApplicationState: IpcExecuteApplicationStateNames.Applied);
    }

    private static OperationExecutionOperationResult CreateOpResult ()
    {
        return new OperationExecutionOperationResult(
            OpId: "query-1",
            Op: UcliPrimitiveOperationNames.AssetsFind,
            Phase: IpcExecuteOperationPhaseNames.Plan,
            Applied: true,
            Changed: false,
            Touched: []);
    }

    private static ReadIndexInfo CreateReadIndexInfo ()
    {
        return new ReadIndexInfo(
            Used: true,
            Hit: true,
            Source: ReadIndexInfoSource.Index,
            Freshness: IndexFreshness.Fresh,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
            FallbackReason: null);
    }
}
