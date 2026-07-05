using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunPathNormalizer : ITestRunPathNormalizer
{
    private readonly TestRunPathNormalizationResult? result;

    public StubTestRunPathNormalizer ()
    {
    }

    public StubTestRunPathNormalizer (TestRunPathNormalizationResult result)
    {
        this.result = result;
    }

    public TestRunPathNormalizationResult TryNormalizeRepositoryPath (
        string repositoryRoot,
        string path)
    {
        return result ?? TestRunPathNormalizationResult.Success(Path.GetFullPath(Path.Combine(repositoryRoot, path)));
    }
}
