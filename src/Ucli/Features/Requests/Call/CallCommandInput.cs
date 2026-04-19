namespace MackySoft.Ucli.Features.Requests.Call;

/// <summary> Represents one normalized <c>call</c> command input. </summary>
/// <param name="RequestPath"> The optional <c>--requestPath</c> value. </param>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="Mode"> The optional <c>--mode</c> value. </param>
/// <param name="Timeout"> The optional <c>--timeout</c> value in milliseconds. </param>
/// <param name="PlanToken"> The optional <c>--planToken</c> value. </param>
/// <param name="WithPlan"> Whether one plan-equivalent payload should be returned. </param>
/// <param name="AllowDangerous"> Whether dangerous operations are explicitly allowed by CLI input. </param>
/// <param name="FailFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
internal sealed record CallCommandInput (
    string? RequestPath,
    string? ProjectPath,
    string? Mode,
    string? Timeout,
    string? PlanToken,
    bool WithPlan,
    bool AllowDangerous,
    bool FailFast);