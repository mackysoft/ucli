using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Contracts;

/// <summary> Represents preflight-resolved execution context for test-run pipeline. </summary>
/// <param name="Configuration"> The resolved test-run configuration. </param>
/// <param name="Config"> The loaded uCLI configuration used for Unity IPC policy. </param>
/// <param name="Target"> The resolved Unity execution target. </param>
/// <param name="Timeout"> The resolved timeout used for execution and daemon probing. </param>
/// <param name="FailFast"> Whether readiness-gated Unity execution should fail immediately instead of waiting for lifecycle readiness. </param>
internal sealed record TestRunExecutionContext (
    ResolvedTestRunConfiguration Configuration,
    UcliConfig Config,
    UnityExecutionTarget Target,
    TimeSpan Timeout,
    bool FailFast);
