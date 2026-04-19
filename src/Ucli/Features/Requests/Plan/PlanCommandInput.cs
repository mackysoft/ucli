namespace MackySoft.Ucli.Features.Requests.Plan;

/// <summary> Represents one normalized <c>plan</c> command input. </summary>
/// <param name="RequestPath"> The optional <c>--requestPath</c> value. </param>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="Mode"> The optional <c>--mode</c> value. </param>
/// <param name="Timeout"> The optional <c>--timeout</c> value in milliseconds. </param>
/// <param name="ReadIndexMode"> The optional <c>--readIndexMode</c> value. </param>
/// <param name="FailFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
internal sealed record PlanCommandInput (
    string? RequestPath,
    string? ProjectPath,
    string? Mode,
    string? Timeout,
    string? ReadIndexMode,
    bool FailFast);