using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;

/// <summary> Represents one normalized <c>resolve</c> command input. </summary>
internal sealed record ResolveCommandInput (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool FailFast,
    ResolveSelectorInput Selector);