using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

namespace MackySoft.Ucli.Application.Tests;

internal static class TestArtifactPaths
{
    public static ArtifactPaths Create (string artifactsDir)
    {
        return new ArtifactPaths(
            ArtifactsDir: artifactsDir,
            MetaJsonPath: Path.Combine(artifactsDir, "meta.json"),
            ResultsXmlPath: Path.Combine(artifactsDir, "results.xml"),
            EditorLogPath: Path.Combine(artifactsDir, "editor.log"),
            ResultsJsonPath: Path.Combine(artifactsDir, "results.json"),
            SummaryJsonPath: Path.Combine(artifactsDir, "summary.json"));
    }
}
