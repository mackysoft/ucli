using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunArtifactExistenceProbe : ITestRunArtifactExistenceProbe
{
    private readonly bool assumeGeneratedFilesExist;

    private StubTestRunArtifactExistenceProbe (bool assumeGeneratedFilesExist)
    {
        this.assumeGeneratedFilesExist = assumeGeneratedFilesExist;
    }

    public static StubTestRunArtifactExistenceProbe CheckingGeneratedFiles ()
    {
        return new StubTestRunArtifactExistenceProbe(assumeGeneratedFilesExist: false);
    }

    public static StubTestRunArtifactExistenceProbe ReturningSuccess ()
    {
        return new StubTestRunArtifactExistenceProbe(assumeGeneratedFilesExist: true);
    }

    public TestRunArtifactExistenceResult ValidateGeneratedFiles (ArtifactPaths artifactPaths)
    {
        if (assumeGeneratedFilesExist)
        {
            return TestRunArtifactExistenceResult.Success();
        }

        if (!File.Exists(artifactPaths.ResultsXmlPath.Value))
        {
            return TestRunArtifactExistenceResult.Failure(
                $"Unity process completed but results.xml was not generated: {artifactPaths.ResultsXmlPath}");
        }

        if (!File.Exists(artifactPaths.EditorLogPath.Value))
        {
            return TestRunArtifactExistenceResult.Failure(
                $"Unity process completed but editor.log was not generated: {artifactPaths.EditorLogPath}");
        }

        return TestRunArtifactExistenceResult.Success();
    }
}
