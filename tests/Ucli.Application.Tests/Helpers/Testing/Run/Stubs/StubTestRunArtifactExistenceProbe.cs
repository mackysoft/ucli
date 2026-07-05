using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunArtifactExistenceProbe : ITestRunArtifactExistenceProbe
{
    public TestRunArtifactExistenceResult ValidateGeneratedFiles (ArtifactPaths artifactPaths)
    {
        if (!File.Exists(artifactPaths.ResultsXmlPath))
        {
            return TestRunArtifactExistenceResult.Failure(
                $"Unity process completed but results.xml was not generated: {artifactPaths.ResultsXmlPath}");
        }

        if (!File.Exists(artifactPaths.EditorLogPath))
        {
            return TestRunArtifactExistenceResult.Failure(
                $"Unity process completed but editor.log was not generated: {artifactPaths.EditorLogPath}");
        }

        return TestRunArtifactExistenceResult.Success();
    }
}
