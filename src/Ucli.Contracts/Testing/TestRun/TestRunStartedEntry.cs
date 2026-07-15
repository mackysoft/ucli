using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.run.started</c> stream payload. </summary>
public sealed record TestRunStartedEntry
{
    /// <summary> Initializes one validated test-run start entry. </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="TestPlatform" />, <paramref name="AssemblyNames" />, or <paramref name="TestCategories" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="RunId" /> is empty, the test platform has no content, or a collection entry has no content.
    /// </exception>
    [JsonConstructor]
    public TestRunStartedEntry (
        Guid RunId,
        string TestPlatform,
        string? TestFilter,
        IReadOnlyList<string> AssemblyNames,
        IReadOnlyList<string> TestCategories)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.TestPlatform = ContractArgumentGuard.RequireValue(TestPlatform, nameof(TestPlatform));
        this.TestFilter = TestFilter;
        this.AssemblyNames = ContractArgumentGuard.RequireValues(AssemblyNames, nameof(AssemblyNames));
        this.TestCategories = ContractArgumentGuard.RequireValues(TestCategories, nameof(TestCategories));
    }

    public Guid RunId { get; }

    public string TestPlatform { get; }

    public string? TestFilter { get; }

    public IReadOnlyList<string> AssemblyNames { get; }

    public IReadOnlyList<string> TestCategories { get; }
}
