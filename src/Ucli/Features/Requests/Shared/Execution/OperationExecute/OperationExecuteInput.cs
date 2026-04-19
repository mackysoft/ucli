using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Represents normalized input for fixed-operation execution. </summary>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="Mode"> The normalized <c>--mode</c> value. </param>
/// <param name="TimeoutMilliseconds"> The normalized <c>--timeout</c> value in milliseconds. </param>
/// <param name="FailFast"> Whether Unity-side execution should fail immediately instead of waiting for lifecycle readiness. </param>
internal sealed record OperationExecuteInput (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    bool FailFast);