namespace MackySoft.Ucli.TestRun.Artifacts;

/// <summary> Represents fixed artifact file paths for one test-run execution. </summary>
/// <param name="MetaJsonPath"> The <c>meta.json</c> path. </param>
/// <param name="ResultsXmlPath"> The <c>results.xml</c> path. </param>
/// <param name="EditorLogPath"> The <c>editor.log</c> path. </param>
/// <param name="ResultsJsonPath"> The <c>results.json</c> path. </param>
/// <param name="SummaryJsonPath"> The <c>summary.json</c> path. </param>
internal sealed record ArtifactPaths (
    string MetaJsonPath,
    string ResultsXmlPath,
    string EditorLogPath,
    string ResultsJsonPath,
    string SummaryJsonPath);