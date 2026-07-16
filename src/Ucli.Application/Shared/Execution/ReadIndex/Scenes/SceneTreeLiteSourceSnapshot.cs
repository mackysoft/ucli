using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents one validated live scene-tree-lite snapshot. </summary>
internal sealed record SceneTreeLiteSourceSnapshot
{
    /// <summary> Initializes a validated live scene-tree-lite snapshot. </summary>
    public SceneTreeLiteSourceSnapshot (
        DateTimeOffset generatedAtUtc,
        UnityScenePath scenePath,
        IReadOnlyList<SceneTreeLiteNode> roots,
        SceneTreeSourceState sourceState)
    {
        if (generatedAtUtc == default || generatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Snapshot generation timestamp must be a non-default UTC value.", nameof(generatedAtUtc));
        }

        GeneratedAtUtc = generatedAtUtc;
        ScenePath = scenePath ?? throw new ArgumentNullException(nameof(scenePath));
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Any(static node => node is null))
        {
            throw new ArgumentException("Snapshot roots must not contain null elements.", nameof(roots));
        }

        Roots = Array.AsReadOnly(roots.ToArray());
        SourceState = sourceState ?? throw new ArgumentNullException(nameof(sourceState));
    }

    /// <summary> Gets the snapshot generation timestamp. </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary> Gets the normalized project-relative scene path. </summary>
    public UnityScenePath ScenePath { get; }

    /// <summary> Gets the validated scene root nodes. </summary>
    public IReadOnlyList<SceneTreeLiteNode> Roots { get; }

    /// <summary> Gets the validated source state. </summary>
    public SceneTreeSourceState SourceState { get; }

}
