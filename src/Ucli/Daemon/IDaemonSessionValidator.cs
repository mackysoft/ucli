using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Validates daemon session model values before persistence and usage. </summary>
internal interface IDaemonSessionValidator
{
    /// <summary> Validates one daemon session model. </summary>
    /// <param name="session"> The daemon session model. </param>
    /// <param name="sessionPath"> The related session JSON path for diagnostics. </param>
    /// <returns> The structured error when validation fails; otherwise <see langword="null" />. </returns>
    ExecutionError? Validate (
        DaemonSession session,
        string sessionPath);
}