using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Represents normalized common options for typed query commands. </summary>
internal sealed record QueryCommonOptions (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool FailFast);
