
namespace MackySoft.Ucli.Contracts.Tests.Command;

public sealed class ScreenshotArtifactContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ScreenshotArtifactContract_ExposesCanonicalKindAndPngMediaType ()
    {
        Assert.Equal(
            "screenshot",
            TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot));
        Assert.Equal("image/png", ScreenshotArtifactContract.MediaType);
    }
}
