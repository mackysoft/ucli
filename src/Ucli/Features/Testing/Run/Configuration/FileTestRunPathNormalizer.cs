using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Normalizes test-run paths through the local filesystem APIs. </summary>
internal sealed class FileTestRunPathNormalizer : ITestRunPathNormalizer
{
    /// <inheritdoc />
    public TestRunPathNormalizationResult TryNormalizeRepositoryPath (
        string repositoryRoot,
        string path)
    {
        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, path);
        if (result.IsSuccess)
        {
            return TestRunPathNormalizationResult.Success(result.FullPath!);
        }

        return TestRunPathNormalizationResult.Failure(
            MapFailureKind(result.FailureKind),
            result.DiagnosticMessage);
    }

    private static TestRunPathNormalizationFailureKind MapFailureKind (PathNormalizationFailureKind failureKind)
    {
        return failureKind switch
        {
            PathNormalizationFailureKind.EmptyPath => TestRunPathNormalizationFailureKind.EmptyPath,
            PathNormalizationFailureKind.InvalidFormat => TestRunPathNormalizationFailureKind.InvalidFormat,
            PathNormalizationFailureKind.OutsideRepositoryRoot => TestRunPathNormalizationFailureKind.OutsideRepositoryRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "Unsupported path normalization failure kind."),
        };
    }
}
