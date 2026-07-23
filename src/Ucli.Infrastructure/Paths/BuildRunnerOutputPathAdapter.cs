using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Paths;

/// <summary> Adapts the portable build-runner output contract to the current-platform filesystem path contract. </summary>
internal static class BuildRunnerOutputPathAdapter
{
    /// <summary> Converts one guarded build-runner output path at the product-to-filesystem boundary. </summary>
    public static RootRelativePath ToRootRelativePath (BuildRunnerOutputPath path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return RootRelativePath.Parse(path.Value);
    }
}
