namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>test.run</c> IPC request payload. </summary>
/// <param name="TestPlatform"> The Unity test platform value (<c>editmode|playmode</c>). </param>
/// <param name="BuildTarget"> The optional Unity build target used by PlayMode tests. </param>
/// <param name="TestFilter"> The optional Unity test-name filter. </param>
/// <param name="TestCategories"> The optional Unity test-category filters. </param>
/// <param name="AssemblyNames"> The optional Unity test assembly-name filters. </param>
/// <param name="TestSettingsPath"> The optional path to <c>TestSettings.json</c>. </param>
/// <param name="ResultsXmlPath"> The absolute output path for Unity test <c>results.xml</c>. </param>
/// <param name="EditorLogPath"> The absolute output path for extracted <c>editor.log</c>. </param>
public sealed record IpcTestRunRequest (
    string TestPlatform,
    string? BuildTarget,
    string? TestFilter,
    string[] TestCategories,
    string[] AssemblyNames,
    string? TestSettingsPath,
    string ResultsXmlPath,
    string EditorLogPath);