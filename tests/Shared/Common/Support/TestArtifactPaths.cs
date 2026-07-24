using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

namespace MackySoft.Ucli.TestSupport;

internal static class TestArtifactPaths
{
    public static ArtifactPaths Create (string artifactsDir)
    {
        var absoluteArtifactsDir = AbsolutePath.Parse(artifactsDir);

        return new ArtifactPaths(
            ArtifactsDir: absoluteArtifactsDir,
            MetaJsonPath: ContainedPath.Create(absoluteArtifactsDir, RootRelativePath.Parse("meta.json")).Target,
            ResultsXmlPath: ContainedPath.Create(absoluteArtifactsDir, RootRelativePath.Parse("results.xml")).Target,
            EditorLogPath: ContainedPath.Create(absoluteArtifactsDir, RootRelativePath.Parse("editor.log")).Target,
            ResultsJsonPath: ContainedPath.Create(absoluteArtifactsDir, RootRelativePath.Parse("results.json")).Target,
            SummaryJsonPath: ContainedPath.Create(absoluteArtifactsDir, RootRelativePath.Parse("summary.json")).Target);
    }
}
