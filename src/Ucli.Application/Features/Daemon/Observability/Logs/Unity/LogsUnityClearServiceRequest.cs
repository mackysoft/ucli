namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Represents normalized inputs for <c>logs unity clear</c> command execution. </summary>
/// <param name="ProjectPath"> The optional target Unity project path. </param>
/// <param name="TimeoutMilliseconds"> The optional timeout override in milliseconds. </param>
internal sealed record LogsUnityClearServiceRequest (
    string? ProjectPath,
    int? TimeoutMilliseconds);
