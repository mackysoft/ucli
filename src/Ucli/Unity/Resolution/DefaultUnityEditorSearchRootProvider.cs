namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides default Unity editor installation roots for local environments. </summary>
internal sealed class DefaultUnityEditorSearchRootProvider : IUnityEditorSearchRootProvider
{
    private readonly IReadOnlyList<IUnityEditorSearchRootSource> searchRootSources;

    private readonly IUnityPathComparerProvider pathComparerProvider;

    /// <summary> Initializes a new instance of the <see cref="DefaultUnityEditorSearchRootProvider" /> class. </summary>
    public DefaultUnityEditorSearchRootProvider ()
        : this(
            new IUnityEditorSearchRootSource[]
            {
                new WindowsUnityEditorSearchRootSource(),
                new MacUnityEditorSearchRootSource(),
                new LinuxUnityEditorSearchRootSource(),
            },
            new UnityPathComparerProvider())
    {
    }

    /// <summary> Initializes a new instance of the <see cref="DefaultUnityEditorSearchRootProvider" /> class. </summary>
    /// <param name="searchRootSources"> Search-root sources grouped by platform. </param>
    /// <param name="pathComparerProvider"> Path comparer provider dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    internal DefaultUnityEditorSearchRootProvider (
        IReadOnlyList<IUnityEditorSearchRootSource> searchRootSources,
        IUnityPathComparerProvider pathComparerProvider)
    {
        ArgumentNullException.ThrowIfNull(searchRootSources);
        ArgumentNullException.ThrowIfNull(pathComparerProvider);

        for (var index = 0; index < searchRootSources.Count; index++)
        {
            if (searchRootSources[index] is null)
            {
                throw new ArgumentException("Search root sources must not contain null elements.", nameof(searchRootSources));
            }
        }

        this.searchRootSources = searchRootSources;
        this.pathComparerProvider = pathComparerProvider;
    }

    /// <summary> Gets candidate root directory paths used for editor discovery. </summary>
    /// <returns> Candidate root paths in deterministic order. </returns>
    public IReadOnlyList<string> GetSearchRoots ()
    {
        var comparer = pathComparerProvider.GetComparer();
        var deduplicatedRoots = new HashSet<string>(comparer);
        var orderedRoots = new List<string>();

        foreach (var searchRootSource in searchRootSources)
        {
            if (!searchRootSource.IsSupportedCurrentPlatform)
            {
                continue;
            }

            var roots = searchRootSource.GetSearchRoots();
            for (var index = 0; index < roots.Count; index++)
            {
                var root = roots[index];
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                if (deduplicatedRoots.Add(root))
                {
                    orderedRoots.Add(root);
                }
            }
        }

        return orderedRoots.ToArray();
    }
}