using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class ResolveCommandTestData
{
    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    public const string GlobalObjectId = "GlobalObjectId_V1-2-0123456789abcdef0123456789abcdef-4-5";

    private static readonly Guid RequestGuid = Guid.Parse(RequestId);

    public static ResolveServiceResult CreateSuccessResult ()
    {
        return ResolveServiceResultFactory.Success(
            RequestGuid,
            [
                new OperationExecutionOperationResult(
                    OpId: new IpcExecuteStepId("resolve"),
                    Op: UcliPrimitiveOperationNames.Resolve,
                    Phase: IpcExecuteOperationPhase.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [])
                {
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        globalObjectId = GlobalObjectId,
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

    public static ResolveServiceResult CreateFailureResult ()
    {
        return ResolveServiceResultFactory.Failure(
            RequestGuid,
            [],
            [
                ApplicationFailure.InternalError(
                    "Unity execution failed.",
                    opId: new IpcExecuteStepId("resolve")),
            ],
            new ReadIndexInfo(
                Used: true,
                Hit: true,
                Source: ReadIndexInfoSource.Index,
                Freshness: IndexFreshness.Fresh,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                FallbackReason: null));
    }
}
