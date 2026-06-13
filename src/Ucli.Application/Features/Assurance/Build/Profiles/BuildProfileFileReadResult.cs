using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents the result of reading one build profile JSON file. </summary>
internal sealed record BuildProfileFileReadResult (
    string? Json,
    string? DisplayPath,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the read completed successfully. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful profile read result. </summary>
    public static BuildProfileFileReadResult Success (
        string json,
        string displayPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayPath);
        return new BuildProfileFileReadResult(json, displayPath, null);
    }

    /// <summary> Creates a failed profile read result. </summary>
    public static BuildProfileFileReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new BuildProfileFileReadResult(null, null, error);
    }
}
