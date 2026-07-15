using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.case.finished</c> stream payload. </summary>
public sealed record TestCaseFinishedEntry
{
    /// <summary> Initializes one validated test-case completion entry. </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when a required text value or <paramref name="Categories" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="RunId" /> is empty, a required text value has no content, or a category has no content.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="Result" /> is undefined or <paramref name="DurationMilliseconds" /> is negative.
    /// </exception>
    [JsonConstructor]
    public TestCaseFinishedEntry (
        Guid RunId,
        string TestId,
        string TestName,
        string? AssemblyName,
        string TestPlatform,
        IReadOnlyList<string> Categories,
        TestCaseResult Result,
        long DurationMilliseconds,
        string? Message,
        string? StackTrace)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        if (!ContractLiteralCodec.IsDefined(Result))
        {
            throw new ArgumentOutOfRangeException(nameof(Result), Result, "Test-case result must be specified.");
        }

        if (DurationMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DurationMilliseconds),
                DurationMilliseconds,
                "Test-case duration must not be negative.");
        }

        this.RunId = RunId;
        this.TestId = ContractArgumentGuard.RequireValue(TestId, nameof(TestId));
        this.TestName = ContractArgumentGuard.RequireValue(TestName, nameof(TestName));
        this.AssemblyName = AssemblyName is null
            ? null
            : ContractArgumentGuard.RequireValue(AssemblyName, nameof(AssemblyName));
        this.TestPlatform = ContractArgumentGuard.RequireValue(TestPlatform, nameof(TestPlatform));
        this.Categories = ContractArgumentGuard.RequireValues(Categories, nameof(Categories));
        this.Result = Result;
        this.DurationMilliseconds = DurationMilliseconds;
        this.Message = Message;
        this.StackTrace = StackTrace;
    }

    public Guid RunId { get; }

    public string TestId { get; }

    public string TestName { get; }

    public string? AssemblyName { get; }

    public string TestPlatform { get; }

    public IReadOnlyList<string> Categories { get; }

    public TestCaseResult Result { get; }

    public long DurationMilliseconds { get; }

    public string? Message { get; }

    public string? StackTrace { get; }
}
