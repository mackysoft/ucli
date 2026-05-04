namespace MackySoft.Ucli.Skills.Shared;

/// <summary> Represents a SKILL library operation result. </summary>
/// <typeparam name="T"> The successful value type. </typeparam>
/// <param name="Value"> The successful value, or <see langword="null" /> when failed. </param>
/// <param name="Failure"> The failure, or <see langword="null" /> when succeeded. </param>
public sealed record SkillOperationResult<T> (
    T? Value,
    SkillFailure? Failure)
{
    /// <summary> Gets a value indicating whether this result succeeded. </summary>
    public bool IsSuccess => Failure is null;

    /// <summary> Creates a successful result. </summary>
    /// <param name="value"> The successful value. </param>
    /// <returns> The successful result. </returns>
    public static SkillOperationResult<T> Success (T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SkillOperationResult<T>(value, null);
    }

    /// <summary> Creates a failed result. </summary>
    /// <param name="code"> The failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    /// <returns> The failed result. </returns>
    public static SkillOperationResult<T> FailureResult (
        string code,
        string message)
    {
        return new SkillOperationResult<T>(default, SkillFailure.Create(code, message));
    }
}
