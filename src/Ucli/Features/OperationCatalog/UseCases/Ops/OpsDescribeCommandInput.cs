using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops;

/// <summary> Represents normalized input for <c>ops describe</c>. </summary>
/// <param name="OperationName"> The target operation name. </param>
/// <param name="ProjectPath"> The optional project path. </param>
/// <param name="Mode"> The normalized execution-mode value. </param>
/// <param name="TimeoutMilliseconds"> The normalized timeout value in milliseconds. </param>
/// <param name="ReadIndexMode"> The optional normalized read-index mode override. </param>
/// <param name="FailFast"> Whether live source fallback should fail immediately instead of waiting for Unity readiness. </param>
internal sealed record OpsDescribeCommandInput (
    string? OperationName,
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool FailFast = false);
