using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

/// <summary> Represents fixed artifact file paths for one test-run execution. </summary>
internal sealed record ArtifactPaths (
    AbsolutePath ArtifactsDir,
    AbsolutePath MetaJsonPath,
    AbsolutePath ResultsXmlPath,
    AbsolutePath EditorLogPath,
    AbsolutePath ResultsJsonPath,
    AbsolutePath SummaryJsonPath);
