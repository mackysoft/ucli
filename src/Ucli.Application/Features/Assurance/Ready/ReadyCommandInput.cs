using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents normalized input for one <c>ready</c> command execution. </summary>
internal sealed record ReadyCommandInput (
    string? ProjectPath,
    ReadyTarget Target,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool IsReadIndexModeSpecified,
    bool FailFast);
