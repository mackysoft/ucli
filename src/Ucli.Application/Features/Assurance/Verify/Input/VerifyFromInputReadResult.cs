namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents the result of reading <c>verify --from</c> input. </summary>
internal sealed record VerifyFromInputReadResult (
    VerifyFromInput? Input,
    ApplicationFailure? Error)
{
    /// <summary> Gets a value indicating whether input reading succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful result. </summary>
    public static VerifyFromInputReadResult Success (VerifyFromInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new VerifyFromInputReadResult(input, null);
    }

    /// <summary> Creates a failed result. </summary>
    public static VerifyFromInputReadResult Failure (ApplicationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new VerifyFromInputReadResult(null, failure);
    }
}
