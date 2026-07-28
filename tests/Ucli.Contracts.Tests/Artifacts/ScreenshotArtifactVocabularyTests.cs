
namespace MackySoft.Ucli.Contracts.Tests.Artifacts;

public sealed class ScreenshotArtifactVocabularyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ScreenshotArtifactVocabularies_ExposeCanonicalKindAndPngMediaType ()
    {
        Assert.Equal(
            "screenshot",
            TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot));
        Assert.Equal(
            "image/png",
            TextVocabulary.GetText(ScreenshotArtifactMediaType.Png));
    }
}
