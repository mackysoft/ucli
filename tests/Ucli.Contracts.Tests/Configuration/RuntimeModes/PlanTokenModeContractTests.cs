using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class PlanTokenModeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenMode_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)PlanTokenMode.Optional);
        Assert.Equal(1, (int)PlanTokenMode.Required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenModeValues_HasStableStringValues ()
    {
        Assert.Equal("optional", PlanTokenModeValues.Optional);
        Assert.Equal("required", PlanTokenModeValues.Required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenModeCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal(PlanTokenModeValues.Optional, PlanTokenModeCodec.ToValue(PlanTokenMode.Optional));
        Assert.Equal(PlanTokenModeValues.Required, PlanTokenModeCodec.ToValue(PlanTokenMode.Required));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenModeCodec_TryParse_AcceptsKnownValuesCaseInsensitive ()
    {
        Assert.True(PlanTokenModeCodec.TryParse("optional", out var optional));
        Assert.Equal(PlanTokenMode.Optional, optional);
        Assert.True(PlanTokenModeCodec.TryParse("REQUIRED", out var required));
        Assert.Equal(PlanTokenMode.Required, required);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlanTokenModeCodec_TryParse_UnknownValue_ReturnsFalse ()
    {
        Assert.False(PlanTokenModeCodec.TryParse("unsupported", out _));
    }
}
