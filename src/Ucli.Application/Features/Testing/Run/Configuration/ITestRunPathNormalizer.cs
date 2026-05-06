namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Normalizes host filesystem paths used by test-run configuration. </summary>
internal interface ITestRunPathNormalizer
{
    /// <summary> Attempts to normalize one path into a repository-bound full host filesystem path. </summary>
    /// <param name="repositoryRoot"> The repository root path used as the boundary and relative base path. </param>
    /// <param name="path"> The input path value. </param>
    /// <returns> The path normalization result. </returns>
    TestRunPathNormalizationResult TryNormalizeRepositoryPath (
        string repositoryRoot,
        string path);
}
