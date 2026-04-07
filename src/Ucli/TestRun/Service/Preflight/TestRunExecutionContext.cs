using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Service.Preflight;

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