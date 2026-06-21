namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents one build output source candidate for artifact-store ingestion. </summary>
internal sealed record BuildOutputSourceEntry
{
    private BuildOutputSourceEntry (
        string path,
        bool isRunnerOutputRelative)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        IsRunnerOutputRelative = isRunnerOutputRelative;
    }

    /// <summary> Gets the source path value. </summary>
    public string Path { get; }

    /// <summary> Gets a value indicating whether <see cref="Path" /> is relative to <c>RunnerOutputDirectory</c>. </summary>
    public bool IsRunnerOutputRelative { get; }

    /// <summary> Creates an absolute filesystem source path entry. </summary>
    public static BuildOutputSourceEntry FromAbsolutePath (string path)
    {
        return new BuildOutputSourceEntry(path, isRunnerOutputRelative: false);
    }

    /// <summary> Creates a runner-output-relative source path entry. </summary>
    public static BuildOutputSourceEntry FromRunnerOutputRelativePath (string path)
    {
        return new BuildOutputSourceEntry(path, isRunnerOutputRelative: true);
    }
}
