using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Normalizes test-run paths through the local filesystem APIs. </summary>
internal sealed class FileTestRunPathNormalizer : ITestRunPathNormalizer
{
    /// <inheritdoc />
    public bool TryNormalizeFullPath (
        string path,
        out string? normalizedPath,
        out string? errorMessage)
    {
        normalizedPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path value is empty.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (ApplicationPathExceptionClassifier.IsPathFormatException(exception))
        {
            errorMessage = exception.Message;
            return false;
        }
    }
}
