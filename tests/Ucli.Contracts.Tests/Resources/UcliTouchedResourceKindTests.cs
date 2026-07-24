
namespace MackySoft.Ucli.Contracts.Tests.Resources;

public sealed class UcliTouchedResourceKindTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ContractLiterals_AreCanonicalAndExcludeUndefinedZero ()
    {
        Assert.Equal(
            ["scene", "prefab", "asset", "projectSettings"],
            TextVocabulary.GetTexts<UcliTouchedResourceKind>());
        Assert.False(TextVocabulary.IsDefined((UcliTouchedResourceKind)0));
    }
}
