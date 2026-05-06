using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one machine-readable index service error. </summary>
/// <param name="Code"> The error code. </param>
/// <param name="Message"> The user-facing error message. </param>
internal sealed record IndexServiceError (
    UcliErrorCode Code,
    string Message);
