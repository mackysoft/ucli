namespace MackySoft.Ucli.Application.Tests.Shared.Identifiers;

public sealed class GuidGeneratorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Generate_ReturnsNonEmptyGuid ()
    {
        Assert.NotEqual(Guid.Empty, new GuidGenerator().Generate());
    }
}
