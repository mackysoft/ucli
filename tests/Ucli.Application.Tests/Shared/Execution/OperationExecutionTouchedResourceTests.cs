namespace MackySoft.Ucli.Application.Tests.Shared.Execution;

public sealed class OperationExecutionTouchedResourceTests
{
    [Theory]
    [InlineData((UcliTouchedResourceKind)0)]
    [InlineData((UcliTouchedResourceKind)int.MaxValue)]
    [Trait("Size", "Small")]
    public void Constructor_WhenKindIsUndefined_RejectsInvalidValue (UcliTouchedResourceKind kind)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new OperationExecutionTouchedResource(
            kind,
            "Assets/Example.asset",
            AssetGuid: null));

        Assert.Equal("Kind", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../Example.asset")]
    [InlineData("/Assets/Example.asset")]
    [InlineData("Assets\\Example.asset")]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathIsBlank_RejectsInvalidValue (string path)
    {
        var exception = Assert.Throws<ArgumentException>(() => new OperationExecutionTouchedResource(
            UcliTouchedResourceKind.Asset,
            path,
            AssetGuid: null));

        Assert.Equal("Path", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenAssetGuidIsEmpty_RejectsInvalidValue ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new OperationExecutionTouchedResource(
            UcliTouchedResourceKind.Asset,
            "Assets/Example.asset",
            Guid.Empty));

        Assert.Equal("AssetGuid", exception.ParamName);
    }
}
