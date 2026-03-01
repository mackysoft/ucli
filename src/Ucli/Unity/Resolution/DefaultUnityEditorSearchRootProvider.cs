namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides default Unity editor installation roots for local environments. </summary>
internal sealed class DefaultUnityEditorSearchRootProvider : IUnityEditorSearchRootProvider
{
    private readonly string[] cachedSearchRoots;

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

        var searchRootSet = new UnityEditorSearchRootSet(pathComparerProvider.GetComparer());
        for (var index = 0; index < searchRootSources.Count; index++)
        {
            var source = searchRootSources[index];
            if (!source.IsSupportedCurrentPlatform)
            {
                continue;
            }

            source.AppendSearchRoots(searchRootSet);
        }

        cachedSearchRoots = searchRootSet.ToArray();
    }

    /// <summary> Gets candidate root directory paths used for editor discovery. </summary>
    /// <returns> Candidate root paths in deterministic order. </returns>
    public IReadOnlyList<string> GetSearchRoots ()
    {
        return cachedSearchRoots;
    }
}