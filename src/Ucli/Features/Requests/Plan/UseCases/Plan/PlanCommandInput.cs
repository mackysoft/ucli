using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan;

/// <summary> Represents one normalized <c>plan</c> command input. </summary>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="Mode"> The normalized <c>--mode</c> value. </param>
/// <param name="TimeoutMilliseconds"> The normalized <c>--timeout</c> value in milliseconds. </param>
/// <param name="ReadIndexMode"> The optional normalized <c>--readIndexMode</c> override. </param>
/// <param name="FailFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
internal sealed record PlanCommandInput (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool FailFast);
