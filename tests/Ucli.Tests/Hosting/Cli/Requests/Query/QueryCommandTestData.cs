using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class QueryCommandTestData
{
    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    public static QueryServiceResult CreateSuccessResult (string commandName)
    {
        return QueryServiceResultFactory.Success(
            commandName,
            RequestId,
            [
                new OperationExecutionOperationResult(
                    OpId: "assets.find",
                    Op: UcliPrimitiveOperationNames.AssetsFind,
                    Phase: IpcExecuteOperationPhaseNames.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [])
                {
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        matches = Array.Empty<object>(),
                    }),
                },
            ],
            new ReadIndexInfo(
                Used: true,
                Hit: true,
                Source: ReadIndexInfoSource.Index,
                Freshness: IndexFreshness.Fresh,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                FallbackReason: null),
            ProjectIdentityInfoTestFactory.Create());
    }

    public static QueryServiceResult CreateFailureResult (string commandName)
    {
        return QueryServiceResultFactory.Failure(
            commandName,
            RequestId,
            [],
            [
                ApplicationFailure.InternalError(
                    "Unity execution failed.",
                    opId: "assets.find"),
            ],
            "Unity execution failed.",
            new ReadIndexInfo(
                Used: true,
                Hit: true,
                Source: ReadIndexInfoSource.Index,
                Freshness: IndexFreshness.Fresh,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                FallbackReason: null));
    }
}
