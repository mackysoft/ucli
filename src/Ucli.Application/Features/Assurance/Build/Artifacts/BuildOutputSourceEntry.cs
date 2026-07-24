using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents one build output source candidate for artifact-store ingestion. </summary>
internal abstract class BuildOutputSourceEntry
{
    private BuildOutputSourceEntry () { }

    /// <summary> Creates an absolute filesystem source path entry. </summary>
    public static BuildOutputSourceEntry FromAbsolutePath (AbsolutePath path)
    {
        return new Absolute(path);
    }

    /// <summary> Creates a runner-output-relative source path entry. </summary>
    public static BuildOutputSourceEntry FromRunnerOutputRelativePath (BuildRunnerOutputPath path)
    {
        return new RunnerOutputRelative(BuildRunnerOutputPathAdapter.ToRootRelativePath(path));
    }

    /// <summary> Represents one normalized absolute filesystem source path. </summary>
    internal sealed class Absolute : BuildOutputSourceEntry
    {
        internal Absolute (AbsolutePath path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary> Gets the normalized absolute filesystem path. </summary>
        public AbsolutePath Path { get; }
    }

    /// <summary> Represents one source path relative to the runner output directory. </summary>
    internal sealed class RunnerOutputRelative : BuildOutputSourceEntry
    {
        internal RunnerOutputRelative (RootRelativePath path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary> Gets the normalized runner-output-relative path. </summary>
        public RootRelativePath Path { get; }
    }
}
