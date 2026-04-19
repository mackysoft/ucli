namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Builds ordered Unity editor search roots with platform-aware de-duplication. </summary>
internal sealed class UnityEditorSearchRootBuilder
{
    private readonly HashSet<string> deduplicatedRoots;

    private readonly List<string> orderedRoots;

    /// <summary> Initializes a new instance of the <see cref="UnityEditorSearchRootBuilder" /> class. </summary>
    /// <param name="comparer"> The comparer used for de-duplication. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="comparer" /> is <see langword="null" />. </exception>
    public UnityEditorSearchRootBuilder (StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        deduplicatedRoots = new HashSet<string>(comparer);
        orderedRoots = new List<string>();
    }

    /// <summary> Adds one candidate root path into the builder. </summary>
    /// <param name="rootPath"> The candidate root path value. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="rootPath" /> is <see langword="null" />, empty, or whitespace. </exception>
    public void Add (string? rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        if (deduplicatedRoots.Add(rootPath))
        {
            orderedRoots.Add(rootPath);
        }
    }

    /// <summary> Creates an ordered snapshot of current search roots. </summary>
    /// <returns> The ordered de-duplicated root path array. </returns>
    public string[] ToArray ()
    {
        return orderedRoots.ToArray();
    }
}