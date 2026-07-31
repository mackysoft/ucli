namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary> Represents the closed result of one refresh command application handler. </summary>
internal sealed record RefreshExecutionResult
{
    private const string SuccessMessage = "uCLI refresh completed.";
    private const string FailureMessage = "uCLI refresh failed.";

    private RefreshExecutionResult (
        RefreshExecutionOutput? output,
        RefreshExecutionErrorOutput? errorOutput,
        IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if ((output is not null) == (failures.Count != 0))
        {
            throw new ArgumentException(
                "A refresh result must contain either one successful output or at least one failure.",
                nameof(failures));
        }

        if (output is not null && errorOutput is not null)
        {
            throw new ArgumentException(
                "A successful refresh result must not contain an error output.",
                nameof(errorOutput));
        }

        Output = output;
        ErrorOutput = errorOutput;
        Failures = failures.Count == 0 ? [] : failures.ToArray();
    }

    /// <summary> Gets the successful output, or <see langword="null" /> after failure. </summary>
    public RefreshExecutionOutput? Output { get; }

    /// <summary> Gets confirmed failure details, or <see langword="null" /> before project resolution. </summary>
    public RefreshExecutionErrorOutput? ErrorOutput { get; }

    /// <summary> Gets the classified failures. </summary>
    public IReadOnlyList<ApplicationFailure> Failures { get; }

    /// <summary> Gets whether the handler completed with a typed successful result. </summary>
    public bool IsSuccess => Output is not null && Failures.Count == 0;

    /// <summary> Gets the user-facing result message. </summary>
    public string Message => IsSuccess
        ? SuccessMessage
        : Failures.FirstOrDefault()?.Message ?? FailureMessage;

    /// <summary> Creates one successful result. </summary>
    public static RefreshExecutionResult Success (RefreshExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new RefreshExecutionResult(output, errorOutput: null, []);
    }

    /// <summary> Creates one failed result. </summary>
    public static RefreshExecutionResult Failure (
        ApplicationFailure failure,
        RefreshExecutionErrorOutput? errorOutput = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return Failure([failure], errorOutput);
    }

    /// <summary> Creates one failed result. </summary>
    public static RefreshExecutionResult Failure (
        IReadOnlyList<ApplicationFailure> failures,
        RefreshExecutionErrorOutput? errorOutput = null)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
        {
            throw new ArgumentException("Refresh failures must not be empty.", nameof(failures));
        }

        return new RefreshExecutionResult(output: null, errorOutput, failures);
    }
}
