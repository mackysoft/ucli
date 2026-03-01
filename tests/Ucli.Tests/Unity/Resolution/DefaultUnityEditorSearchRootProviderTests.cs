namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.UnityProject.Resolution;

public sealed class DefaultUnityEditorSearchRootProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void GetSearchRoots_UsesOnlySupportedSources ()
    {
        var provider = new DefaultUnityEditorSearchRootProvider(
            new IUnityEditorSearchRootSource[]
            {
                new StubSearchRootSource(true, "/first"),
                new StubSearchRootSource(false, "/ignored"),
                new StubSearchRootSource(true, "/second"),
            },
            new StubPathComparerProvider(StringComparer.Ordinal));

        var roots = provider.GetSearchRoots();

        Assert.Equal(
            new[]
            {
                "/first",
                "/second",
            },
            roots);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetSearchRoots_DeduplicatesRootsUsingComparerProvider ()
    {
        var provider = new DefaultUnityEditorSearchRootProvider(
            new IUnityEditorSearchRootSource[]
            {
                new StubSearchRootSource(true, "/Root", "/Another"),
                new StubSearchRootSource(true, "/root", "/ANOTHER", string.Empty),
            },
            new StubPathComparerProvider(StringComparer.OrdinalIgnoreCase));

        var roots = provider.GetSearchRoots();

        Assert.Equal(
            new[]
            {
                "/Root",
                "/Another",
            },
            roots);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetSearchRoots_ReturnsCachedInstance ()
    {
        var provider = new DefaultUnityEditorSearchRootProvider(
            new IUnityEditorSearchRootSource[]
            {
                new StubSearchRootSource(true, "/cached"),
            },
            new StubPathComparerProvider(StringComparer.Ordinal));

        var first = provider.GetSearchRoots();
        var second = provider.GetSearchRoots();

        Assert.Same(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetComparer_ReturnsPlatformAwareComparer ()
    {
        var provider = new UnityPathComparerProvider();

        var comparer = provider.GetComparer();

        Assert.Equal(OperatingSystem.IsWindows(), comparer.Equals("A", "a"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LinuxSource_OnLinux_IncludesCaseVariantOptRoots ()
    {
        var source = new LinuxUnityEditorSearchRootSource();
        var searchRootSet = new UnityEditorSearchRootSet(StringComparer.Ordinal);
        source.AppendSearchRoots(searchRootSet);
        var roots = searchRootSet.ToArray();

        if (source.IsSupportedCurrentPlatform)
        {
            Assert.Contains("/opt/Unity/Hub/Editor", roots, StringComparer.Ordinal);
            Assert.Contains("/opt/unity/hub/editor", roots, StringComparer.Ordinal);
            return;
        }

        Assert.Empty(roots);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MacSource_OnMac_IncludesApplicationsRoots ()
    {
        var source = new MacUnityEditorSearchRootSource();
        var searchRootSet = new UnityEditorSearchRootSet(StringComparer.Ordinal);
        source.AppendSearchRoots(searchRootSet);
        var roots = searchRootSet.ToArray();

        if (source.IsSupportedCurrentPlatform)
        {
            Assert.Contains("/Applications/Unity/Hub/Editor", roots, StringComparer.Ordinal);
            Assert.Contains("/Applications/Unity/Editor", roots, StringComparer.Ordinal);
            return;
        }

        Assert.Empty(roots);
    }

    private sealed class StubSearchRootSource : IUnityEditorSearchRootSource
    {
        private readonly IReadOnlyList<string> roots;

        public StubSearchRootSource (
            bool isSupportedCurrentPlatform,
            params string[] roots)
        {
            IsSupportedCurrentPlatform = isSupportedCurrentPlatform;
            this.roots = roots;
        }

        public bool IsSupportedCurrentPlatform { get; }

        public void AppendSearchRoots (UnityEditorSearchRootSet searchRootSet)
        {
            ArgumentNullException.ThrowIfNull(searchRootSet);

            for (var index = 0; index < roots.Count; index++)
            {
                searchRootSet.Add(roots[index]);
            }
        }
    }

    private sealed class StubPathComparerProvider : IUnityPathComparerProvider
    {
        private readonly StringComparer comparer;

        public StubPathComparerProvider (StringComparer comparer)
        {
            this.comparer = comparer;
        }

        public StringComparer GetComparer ()
        {
            return comparer;
        }
    }
}