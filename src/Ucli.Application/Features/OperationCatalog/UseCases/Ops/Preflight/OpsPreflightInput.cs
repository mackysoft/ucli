using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Preflight;

/// <summary> Represents normalized input shared by <c>ops</c> command preflight. </summary>
/// <param name="ProjectPath"> The optional project path. </param>
/// <param name="Mode"> The normalized execution-mode value. </param>
/// <param name="TimeoutMilliseconds"> The normalized timeout value in milliseconds. </param>
/// <param name="ReadIndexMode"> The optional normalized read-index mode override. </param>
/// <param name="FailFast"> Whether live source fallback should fail immediately instead of waiting for Unity readiness. </param>
internal sealed record OpsPreflightInput (
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds,
    ReadIndexMode? ReadIndexMode,
    bool FailFast = false)
{
    /// <summary> Creates a preflight input from one command input source. </summary>
    public static OpsPreflightInput From (IOpsPreflightInputSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new OpsPreflightInput(
            ProjectPath: source.ProjectPath,
            Mode: source.Mode,
            TimeoutMilliseconds: source.TimeoutMilliseconds,
            ReadIndexMode: source.ReadIndexMode,
            FailFast: source.FailFast);
    }
}
