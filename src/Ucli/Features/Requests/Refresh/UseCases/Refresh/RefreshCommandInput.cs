using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Requests.Refresh.UseCases.Refresh;

/// <summary> Represents one normalized <c>refresh</c> command input. </summary>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="Mode"> The normalized <c>--mode</c> value. </param>
/// <param name="TimeoutMilliseconds"> The normalized <c>--timeout</c> value in milliseconds. </param>
/// <param name="FailFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
internal sealed record RefreshCommandInput (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    bool FailFast);
