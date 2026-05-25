using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Execution;

/// <summary> Validates generated test-run artifact existence without binding the application layer to host file APIs. </summary>
internal interface ITestRunArtifactExistenceProbe
{
    /// <summary> Validates that Unity generated all required run artifacts. </summary>
    /// <param name="artifactPaths"> The artifact paths prepared for the run. </param>
    /// <returns> The artifact existence validation result. </returns>
    TestRunArtifactExistenceResult ValidateGeneratedFiles (ArtifactPaths artifactPaths);
}
