using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Represents the result of reading raw test-run profile JSON. </summary>
/// <param name="Json"> The raw profile JSON text on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured read error on failure; otherwise <see langword="null" />. </param>
internal sealed record TestRunProfileJsonReadResult (
    string? Json,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the profile JSON read succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful profile JSON read result. </summary>
    /// <param name="json"> The raw profile JSON text. </param>
    /// <returns> The successful result. </returns>
    public static TestRunProfileJsonReadResult Success (string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return new TestRunProfileJsonReadResult(json, null);
    }

    /// <summary> Creates a failed profile JSON read result. </summary>
    /// <param name="error"> The structured read error. </param>
    /// <returns> The failed result. </returns>
    public static TestRunProfileJsonReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new TestRunProfileJsonReadResult(null, error);
    }
}
