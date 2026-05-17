namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents final editor lifecycle evidence grouped under <c>payload.compile.lifecycle</c>. </summary>
internal sealed record CompileLifecycleOutput (
    string? ServerVersion,
    string? UnityVersion,
    string? EditorMode,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    CompilePrimaryDiagnosticOutput? PrimaryDiagnostic);
