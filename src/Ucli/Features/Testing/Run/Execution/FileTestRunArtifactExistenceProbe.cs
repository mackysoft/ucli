using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;

namespace MackySoft.Ucli.Features.Testing.Run.Execution;

/// <summary> Validates generated test-run artifact existence through the host file system. </summary>
internal sealed class FileTestRunArtifactExistenceProbe : ITestRunArtifactExistenceProbe
{
    /// <inheritdoc />
    public TestRunArtifactExistenceResult ValidateGeneratedFiles (ArtifactPaths artifactPaths)
    {
        ArgumentNullException.ThrowIfNull(artifactPaths);

        if (!TestRunArtifactValidator.TryValidateGeneratedFiles(artifactPaths, out var errorMessage))
        {
            return TestRunArtifactExistenceResult.Failure(errorMessage!);
        }

        return TestRunArtifactExistenceResult.Success();
    }
}
