using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Testing.Profiles;

/// <summary> Represents the result of test-profile initialization execution. </summary>
/// <param name="Output"> The test-profile initialization output values on success. </param>
/// <param name="Error"> The structured error on failure. </param>
internal sealed record TestProfileInitExecutionResult (
    TestProfileInitExecutionOutput? Output,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether test-profile initialization execution succeeded. </summary>
    public bool IsSuccess => Output is not null && Error is null;

    /// <summary> Creates a successful test-profile initialization execution result. </summary>
    /// <param name="output"> The test-profile initialization output values. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="output" /> is <see langword="null" />. </exception>
    public static TestProfileInitExecutionResult Success (TestProfileInitExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new TestProfileInitExecutionResult(output, null);
    }

    /// <summary> Creates a failed test-profile initialization execution result. </summary>
    /// <param name="error"> The structured initialization error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static TestProfileInitExecutionResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new TestProfileInitExecutionResult(null, error);
    }
}