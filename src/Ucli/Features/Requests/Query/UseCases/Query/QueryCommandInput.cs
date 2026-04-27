using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one normalized typed-query command input. </summary>
internal sealed record QueryCommandInput (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool FailFast,
    QueryOperationRequest Operation);
