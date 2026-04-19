using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Runtime;

/// <summary> Validates daemon session model values before persistence and usage. </summary>
internal interface IDaemonSessionValidator
{
    /// <summary> Validates one daemon session model. </summary>
    /// <param name="session"> The daemon session model. </param>
    /// <param name="sessionPath"> The related session JSON path for diagnostics. </param>
    /// <param name="error"> The structured validation error when validation fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
    bool TryValidate (
        DaemonSession session,
        string sessionPath,
        [NotNullWhen(false)] out ExecutionError? error);
}