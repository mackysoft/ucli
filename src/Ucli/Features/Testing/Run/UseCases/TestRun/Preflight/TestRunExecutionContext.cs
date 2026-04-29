using MackySoft.Ucli.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Preflight;

/// <summary> Represents preflight-resolved execution context for test-run pipeline. </summary>
/// <param name="Configuration"> The resolved test-run configuration. </param>
/// <param name="Target"> The resolved Unity execution target. </param>
/// <param name="Timeout"> The resolved timeout used for execution and daemon probing. </param>
/// <param name="FailFast"> Whether daemon-backed execution should fail immediately instead of waiting for lifecycle readiness. </param>
internal sealed record TestRunExecutionContext (
    ResolvedTestRunConfiguration Configuration,
    UnityExecutionTarget Target,
    TimeSpan Timeout,
    bool FailFast);
