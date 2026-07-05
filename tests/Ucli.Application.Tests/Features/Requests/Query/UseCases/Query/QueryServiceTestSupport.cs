using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal static class QueryServiceTestSupport
{
    internal static readonly ProjectContext QueryProjectContext = ProjectContextTestFactory.CreateRepositoryFixtureProject(
        UcliConfig.CreateDefault() with
        {
            IpcDefaultTimeoutMilliseconds = 1234,
        });

    internal static QueryCommandInput CreateInput (
        QueryOperationRequest operation,
        ReadIndexMode? readIndexMode = null,
        bool failFast = false)
    {
        return new QueryCommandInput(
            ProjectPath: "/repo/UnityProject",
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 1234,
            ReadIndexMode: readIndexMode,
            FailFast: failFast,
            Operation: operation);
    }
}
