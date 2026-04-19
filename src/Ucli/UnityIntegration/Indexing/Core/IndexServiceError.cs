namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Represents one machine-readable index service error. </summary>
/// <param name="Code"> The error code. </param>
/// <param name="Message"> The user-facing error message. </param>
internal sealed record IndexServiceError (
    string Code,
    string Message);