using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents one <c>verify --from</c> input file read result. </summary>
internal sealed record VerifyFromInputFileReadResult (
    string? Json,
    ApplicationFailure? Error)
{
    /// <summary> Gets a value indicating whether the file was read successfully. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful read result. </summary>
    public static VerifyFromInputFileReadResult Success (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return new VerifyFromInputFileReadResult(json, null);
    }

    /// <summary> Creates a failed read result. </summary>
    public static VerifyFromInputFileReadResult Failure (ApplicationFailure error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new VerifyFromInputFileReadResult(null, error);
    }
}
