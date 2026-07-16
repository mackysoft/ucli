using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents one build output source candidate for artifact-store ingestion. </summary>
internal abstract class BuildOutputSourceEntry
{
    private BuildOutputSourceEntry () { }

    /// <summary> Creates an absolute filesystem source path entry. </summary>
    public static BuildOutputSourceEntry FromAbsolutePath (string path)
    {
        return new Absolute(path);
    }

    private static string NormalizeAbsolutePath (string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Build output source path must be fully qualified.", nameof(path));
        }

        return Path.GetFullPath(path);
    }

    /// <summary> Creates a runner-output-relative source path entry. </summary>
    public static BuildOutputSourceEntry FromRunnerOutputRelativePath (BuildRunnerOutputPath path)
    {
        return new RunnerOutputRelative(path);
    }

    /// <summary> Represents one normalized absolute filesystem source path. </summary>
    internal sealed class Absolute : BuildOutputSourceEntry
    {
        internal Absolute (string path)
        {
            Path = NormalizeAbsolutePath(path);
        }

        /// <summary> Gets the normalized absolute filesystem path. </summary>
        public string Path { get; }
    }

    /// <summary> Represents one source path relative to the runner output directory. </summary>
    internal sealed class RunnerOutputRelative : BuildOutputSourceEntry
    {
        internal RunnerOutputRelative (BuildRunnerOutputPath path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary> Gets the normalized runner-output-relative path. </summary>
        public BuildRunnerOutputPath Path { get; }
    }
}
