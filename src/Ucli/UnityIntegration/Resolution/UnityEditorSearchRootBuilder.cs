using MackySoft.FileSystem;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Builds ordered Unity editor search roots with platform-aware de-duplication. </summary>
internal sealed class UnityEditorSearchRootBuilder
{
    private readonly HashSet<AbsolutePath> deduplicatedRoots = new();

    private readonly List<AbsolutePath> orderedRoots = new();

    /// <summary> Initializes a new instance of the <see cref="UnityEditorSearchRootBuilder" /> class. </summary>
    public UnityEditorSearchRootBuilder ()
    {
    }

    /// <summary> Adds one candidate root path into the builder. </summary>
    /// <param name="rootPath"> The candidate root path value. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="rootPath" /> is <see langword="null" />. </exception>
    public void Add (AbsolutePath rootPath)
    {
        ArgumentNullException.ThrowIfNull(rootPath);

        if (deduplicatedRoots.Add(rootPath))
        {
            orderedRoots.Add(rootPath);
        }
    }

    /// <summary> Creates an ordered snapshot of current search roots. </summary>
    /// <returns> The ordered de-duplicated root path array. </returns>
    public AbsolutePath[] ToArray ()
    {
        return orderedRoots.ToArray();
    }
}
