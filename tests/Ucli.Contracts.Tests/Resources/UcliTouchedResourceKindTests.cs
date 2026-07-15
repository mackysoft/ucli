using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Resources;

public sealed class UcliTouchedResourceKindTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ContractLiterals_AreCanonicalAndExcludeUndefinedZero ()
    {
        Assert.Equal(
            ["scene", "prefab", "asset", "projectSettings"],
            ContractLiteralCodec.GetLiterals<UcliTouchedResourceKind>());
        Assert.False(ContractLiteralCodec.IsDefined((UcliTouchedResourceKind)0));
    }
}
