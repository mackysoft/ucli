using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.run.started</c> stream payload. </summary>
public sealed record TestRunStartedEntry
{
    /// <summary> Initializes one test-run start entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public TestRunStartedEntry (
        Guid RunId,
        string TestPlatform,
        string? TestFilter,
        string[] AssemblyNames,
        string[] TestCategories)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.TestPlatform = TestPlatform;
        this.TestFilter = TestFilter;
        this.AssemblyNames = AssemblyNames;
        this.TestCategories = TestCategories;
    }

    public Guid RunId { get; }

    public string TestPlatform { get; }

    public string? TestFilter { get; }

    public string[] AssemblyNames { get; }

    public string[] TestCategories { get; }
}
