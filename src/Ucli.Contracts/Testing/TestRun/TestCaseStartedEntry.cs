using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.case.started</c> stream payload. </summary>
public sealed record TestCaseStartedEntry
{
    /// <summary> Initializes one validated test-case start entry. </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when a required text value or <paramref name="Categories" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="RunId" /> is empty, a required text value has no content, or a category has no content.
    /// </exception>
    [JsonConstructor]
    public TestCaseStartedEntry (
        Guid RunId,
        string TestId,
        string TestName,
        string? AssemblyName,
        string TestPlatform,
        IReadOnlyList<string> Categories)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.TestId = ContractArgumentGuard.RequireValue(TestId, nameof(TestId));
        this.TestName = ContractArgumentGuard.RequireValue(TestName, nameof(TestName));
        this.AssemblyName = AssemblyName is null
            ? null
            : ContractArgumentGuard.RequireValue(AssemblyName, nameof(AssemblyName));
        this.TestPlatform = ContractArgumentGuard.RequireValue(TestPlatform, nameof(TestPlatform));
        this.Categories = ContractArgumentGuard.RequireValues(Categories, nameof(Categories));
    }

    public Guid RunId { get; }

    public string TestId { get; }

    public string TestName { get; }

    public string? AssemblyName { get; }

    public string TestPlatform { get; }

    public IReadOnlyList<string> Categories { get; }
}
