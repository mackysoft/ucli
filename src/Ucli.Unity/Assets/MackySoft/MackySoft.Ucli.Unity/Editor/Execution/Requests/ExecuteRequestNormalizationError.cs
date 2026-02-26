namespace MackySoft.Ucli.Unity.Execution.Requests;

/// <summary> Represents one structured normalization error. </summary>
/// <param name="Code"> The machine-readable error code. </param>
/// <param name="Message"> The user-facing error message. </param>
/// <param name="OpId"> The related operation identifier when available; otherwise <see langword="null" />. </param>
internal sealed record ExecuteRequestNormalizationError (
    string Code,
    string Message,
    string? OpId);
