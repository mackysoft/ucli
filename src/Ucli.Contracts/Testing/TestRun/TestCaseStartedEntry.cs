using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents the <c>test.case.started</c> stream payload. </summary>
public sealed record TestCaseStartedEntry
{
    /// <summary> Initializes one test-case start entry for a non-empty run identifier. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public TestCaseStartedEntry (
        Guid RunId,
        string TestId,
        string TestName,
        string? AssemblyName,
        string TestPlatform,
        string[] Categories)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.TestId = TestId;
        this.TestName = TestName;
        this.AssemblyName = AssemblyName;
        this.TestPlatform = TestPlatform;
        this.Categories = Categories;
    }

    public Guid RunId { get; }

    public string TestId { get; }

    public string TestName { get; }

    public string? AssemblyName { get; }

    public string TestPlatform { get; }

    public string[] Categories { get; }
}
