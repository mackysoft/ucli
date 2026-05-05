namespace MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

/// <summary> Represents fixed artifact file paths for one test-run execution. </summary>
internal sealed record ArtifactPaths
{
    private const string MetaJsonFileName = "meta.json";

    private const string ResultsXmlFileName = "results.xml";

    private const string EditorLogFileName = "editor.log";

    private const string ResultsJsonFileName = "results.json";

    private const string SummaryJsonFileName = "summary.json";

    /// <summary> Initializes a new instance of the <see cref="ArtifactPaths" /> class from one artifacts directory. </summary>
    /// <param name="artifactsDir"> The run artifacts directory path. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="artifactsDir" /> is <see langword="null" />, empty, or whitespace. </exception>
    public ArtifactPaths (string artifactsDir)
    {
        if (string.IsNullOrWhiteSpace(artifactsDir))
        {
            throw new ArgumentException("Artifacts directory must not be empty.", nameof(artifactsDir));
        }

        ArtifactsDir = artifactsDir;
        MetaJsonPath = Path.Combine(artifactsDir, MetaJsonFileName);
        ResultsXmlPath = Path.Combine(artifactsDir, ResultsXmlFileName);
        EditorLogPath = Path.Combine(artifactsDir, EditorLogFileName);
        ResultsJsonPath = Path.Combine(artifactsDir, ResultsJsonFileName);
        SummaryJsonPath = Path.Combine(artifactsDir, SummaryJsonFileName);
    }

    /// <summary> Gets the run artifacts directory path. </summary>
    public string ArtifactsDir { get; }

    /// <summary> Gets the <c>meta.json</c> path. </summary>
    public string MetaJsonPath { get; }

    /// <summary> Gets the <c>results.xml</c> path. </summary>
    public string ResultsXmlPath { get; }

    /// <summary> Gets the <c>editor.log</c> path. </summary>
    public string EditorLogPath { get; }

    /// <summary> Gets the <c>results.json</c> path. </summary>
    public string ResultsJsonPath { get; }

    /// <summary> Gets the <c>summary.json</c> path. </summary>
    public string SummaryJsonPath { get; }
}
