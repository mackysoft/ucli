using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;

/// <summary> Represents normalized input for <c>ops list</c>. </summary>
/// <param name="ProjectPath"> The optional project path. </param>
/// <param name="Mode"> The normalized execution-mode value. </param>
/// <param name="TimeoutMilliseconds"> The normalized timeout value in milliseconds. </param>
/// <param name="ReadIndexMode"> The optional normalized read-index mode override. </param>
/// <param name="FailFast"> Whether live source fallback should fail immediately instead of waiting for Unity readiness. </param>
internal sealed record OpsCommandInput (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool FailFast = false);
