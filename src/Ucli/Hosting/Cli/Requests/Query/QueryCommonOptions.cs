using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Represents normalized common options for typed query commands. </summary>
internal sealed record QueryCommonOptions (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool FailFast);
