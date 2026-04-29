namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.UnityIntegration.Resolution;

public sealed class UnityPathComparerProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void GetComparer_ReturnsPlatformAwareComparer ()
    {
        var provider = new UnityPathComparerProvider();

        var comparer = provider.GetComparer();

        Assert.Equal(OperatingSystem.IsWindows(), comparer.Equals("A", "a"));
    }
}
