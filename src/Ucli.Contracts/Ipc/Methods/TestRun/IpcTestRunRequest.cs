using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>test.run</c> IPC request payload. </summary>
public sealed record IpcTestRunRequest
{
    /// <summary> Initializes a test-run request for one non-empty run identifier. </summary>
    /// <param name="TestPlatform"> The Unity test platform value (<c>editmode|playmode|&lt;BuildTarget&gt;</c>). </param>
    /// <param name="TestFilter"> The optional Unity test-name filter. </param>
    /// <param name="TestCategories"> The Unity test-category filters. Entries must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="AssemblyNames"> The Unity test assembly-name filters. Entries must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="RunId"> The uCLI run identifier used to correlate live progress and artifacts. </param>
    /// <param name="FailFast"> Whether execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="TestCategories" /> or <paramref name="AssemblyNames" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty or a filter collection contains a <see langword="null" />, empty, or whitespace entry. </exception>
    [JsonConstructor]
    public IpcTestRunRequest (
        string TestPlatform,
        string? TestFilter,
        IReadOnlyList<string> TestCategories,
        IReadOnlyList<string> AssemblyNames,
        Guid RunId,
        bool FailFast)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.TestPlatform = TestPlatform;
        this.TestFilter = TestFilter;
        this.TestCategories = ContractArgumentGuard.RequireValues(TestCategories, nameof(TestCategories));
        this.AssemblyNames = ContractArgumentGuard.RequireValues(AssemblyNames, nameof(AssemblyNames));
        this.FailFast = FailFast;
        this.RunId = RunId;
    }

    /// <summary> Gets the Unity test platform value. </summary>
    public string TestPlatform { get; }

    /// <summary> Gets the optional Unity test-name filter. </summary>
    public string? TestFilter { get; }

    /// <summary> Gets the validated, read-only test-category filters. </summary>
    public IReadOnlyList<string> TestCategories { get; }

    /// <summary> Gets the validated, read-only assembly-name filters. </summary>
    public IReadOnlyList<string> AssemblyNames { get; }

    /// <summary> Gets whether lifecycle readiness should fail immediately when unavailable. </summary>
    public bool FailFast { get; }

    /// <summary> Gets the non-empty run identifier used for progress and artifact correlation. </summary>
    public Guid RunId { get; }

}
