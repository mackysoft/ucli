namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.UnityIntegration.Resolution;

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
                new StubSearchRootSource(true, "/root", "/ANOTHER"),
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

        public void AppendSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder)
        {
            ArgumentNullException.ThrowIfNull(searchRootBuilder);

            for (var index = 0; index < roots.Count; index++)
            {
                searchRootBuilder.Add(roots[index]);
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
