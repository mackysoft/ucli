namespace MackySoft.Ucli.UnityIntegration.Resolution;

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
    /// <exception cref="ArgumentException"> Thrown when <paramref name="searchRootSources" /> contains <see langword="null" /> elements. </exception>
    internal DefaultUnityEditorSearchRootProvider (
        IReadOnlyList<IUnityEditorSearchRootSource> searchRootSources,
        IUnityPathComparerProvider pathComparerProvider)
    {
        ArgumentNullException.ThrowIfNull(searchRootSources);
        ArgumentNullException.ThrowIfNull(pathComparerProvider);

        for (var i = 0; i < searchRootSources.Count; i++)
        {
            if (searchRootSources[i] is null)
            {
                throw new ArgumentException("Search root sources must not contain null elements.", nameof(searchRootSources));
            }
        }

        var searchRootBuilder = new UnityEditorSearchRootBuilder(pathComparerProvider.GetComparer());
        for (var i = 0; i < searchRootSources.Count; i++)
        {
            var source = searchRootSources[i];
            if (!source.IsSupportedCurrentPlatform)
            {
                continue;
            }

            source.AppendSearchRoots(searchRootBuilder);
        }

        cachedSearchRoots = searchRootBuilder.ToArray();
    }

    /// <summary> Gets candidate root directory paths used for editor discovery. </summary>
    /// <returns> Candidate root paths in deterministic order. </returns>
    public IReadOnlyList<string> GetSearchRoots ()
    {
        return cachedSearchRoots;
    }
}