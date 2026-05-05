namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

/// <summary> Represents one normalized <c>call</c> command input. </summary>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="Mode"> The normalized <c>--mode</c> value. </param>
/// <param name="TimeoutMilliseconds"> The normalized <c>--timeout</c> value in milliseconds. </param>
/// <param name="PlanToken"> The optional <c>--planToken</c> value. </param>
/// <param name="WithPlan"> Whether one plan-equivalent payload should be returned. </param>
/// <param name="AllowDangerous"> Whether dangerous operations are explicitly allowed by CLI input. </param>
/// <param name="FailFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
/// <param name="RequestJson"> The raw request JSON read by the CLI host. </param>
internal sealed record CallCommandInput (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    string? PlanToken,
    bool WithPlan,
    bool AllowDangerous,
    bool FailFast,
    string RequestJson);
