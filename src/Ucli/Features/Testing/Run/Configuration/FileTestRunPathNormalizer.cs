using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Normalizes test-run paths through the local filesystem APIs. </summary>
internal sealed class FileTestRunPathNormalizer : ITestRunPathNormalizer
{
    /// <inheritdoc />
    public bool TryNormalizeRepositoryPath (
        string repositoryRoot,
        string path,
        out string? normalizedPath,
        out string? errorMessage)
    {
        normalizedPath = null;
        errorMessage = null;

        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, path);
        if (result.IsSuccess)
        {
            normalizedPath = result.FullPath;
            return true;
        }

        errorMessage = result.FailureKind switch
        {
            PathNormalizationFailureKind.EmptyPath => "Path value is empty.",
            PathNormalizationFailureKind.OutsideRepositoryRoot => "Path must be under the repository root.",
            _ => result.DiagnosticMessage,
        };
        return false;
    }
}
