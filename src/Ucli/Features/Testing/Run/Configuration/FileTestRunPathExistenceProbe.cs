using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Probes test-run configuration paths through the local filesystem. </summary>
internal sealed class FileTestRunPathExistenceProbe : ITestRunPathExistenceProbe
{
    /// <inheritdoc />
    public bool FileExists (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return File.Exists(path);
    }
}
