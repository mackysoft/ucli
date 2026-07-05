using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubTestRunPathExistenceProbe : ITestRunPathExistenceProbe
{
    private readonly HashSet<string> existingPaths;

    public StubTestRunPathExistenceProbe (params string[] existingPaths)
    {
        this.existingPaths = existingPaths
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.Ordinal);
    }

    public bool FileExists (string path)
    {
        return existingPaths.Contains(Path.GetFullPath(path));
    }
}
