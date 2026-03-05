using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Logs;

/// <summary> Validates one <c>logs daemon</c> request and resolves derived runtime values. </summary>
internal interface ILogsDaemonRequestValidator
{
    /// <summary> Validates request values and resolves stream runtime options. </summary>
    /// <param name="request"> The command request values. </param>
    /// <param name="validatedRequest"> The validated runtime options when validation succeeds. </param>
    /// <param name="error"> Structured invalid-argument error when validation fails. </param>
    /// <returns> <see langword="true" /> when request is valid; otherwise <see langword="false" />. </returns>
    bool TryValidate (
        LogsDaemonServiceRequest request,
        [NotNullWhen(true)]
        out LogsDaemonValidatedRequest? validatedRequest,
        out ExecutionError? error);
}