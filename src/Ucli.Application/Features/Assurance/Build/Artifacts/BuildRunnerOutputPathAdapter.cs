using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Adapts the portable build-runner wire path to the current-platform filesystem path contract. </summary>
internal static class BuildRunnerOutputPathAdapter
{
    /// <summary> Converts one guarded build-runner output path at the IPC-to-filesystem boundary. </summary>
    public static RootRelativePath ToRootRelativePath (BuildRunnerOutputPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return RootRelativePath.Parse(path.Value);
    }
}
