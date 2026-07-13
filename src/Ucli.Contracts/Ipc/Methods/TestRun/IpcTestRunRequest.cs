using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>test.run</c> IPC request payload. </summary>
public sealed record IpcTestRunRequest
{
    /// <summary> Initializes a test-run request for one non-empty run identifier. </summary>
    /// <param name="TestPlatform"> The Unity test platform value (<c>editmode|playmode|&lt;BuildTarget&gt;</c>). </param>
    /// <param name="TestFilter"> The optional Unity test-name filter. </param>
    /// <param name="TestCategories"> The optional Unity test-category filters. </param>
    /// <param name="AssemblyNames"> The optional Unity test assembly-name filters. </param>
    /// <param name="TestSettingsPath"> The optional path to <c>TestSettings.json</c>. </param>
    /// <param name="ResultsXmlPath"> The absolute output path for Unity test <c>results.xml</c>. </param>
    /// <param name="EditorLogPath"> The absolute output path for extracted <c>editor.log</c>. </param>
    /// <param name="RunId"> The uCLI run identifier used to correlate live progress and artifacts. </param>
    /// <param name="FailFast"> Whether execution should fail immediately instead of waiting for lifecycle readiness. </param>
    /// <param name="TimeoutMilliseconds"> The remaining IPC execution budget used for server-side test cancellation. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public IpcTestRunRequest (
        string TestPlatform,
        string? TestFilter,
        string[] TestCategories,
        string[] AssemblyNames,
        string? TestSettingsPath,
        string ResultsXmlPath,
        string EditorLogPath,
        Guid RunId,
        bool FailFast = false,
        int? TimeoutMilliseconds = null)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.TestPlatform = TestPlatform;
        this.TestFilter = TestFilter;
        this.TestCategories = TestCategories;
        this.AssemblyNames = AssemblyNames;
        this.TestSettingsPath = TestSettingsPath;
        this.ResultsXmlPath = ResultsXmlPath;
        this.EditorLogPath = EditorLogPath;
        this.FailFast = FailFast;
        this.RunId = RunId;
        this.TimeoutMilliseconds = TimeoutMilliseconds;
    }

    public string TestPlatform { get; }

    public string? TestFilter { get; }

    public string[] TestCategories { get; }

    public string[] AssemblyNames { get; }

    public string? TestSettingsPath { get; }

    public string ResultsXmlPath { get; }

    public string EditorLogPath { get; }

    public bool FailFast { get; }

    public Guid RunId { get; }

    public int? TimeoutMilliseconds { get; init; }
}
