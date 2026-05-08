using MackySoft.Ucli.Contracts.Daemon;

namespace MackySoft.Ucli.Contracts.Tests.Daemon;

public sealed class DaemonContractCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void DaemonEditorModeValues_HasStableStringValues ()
    {
        Assert.Equal("batchmode", DaemonEditorModeValues.Batchmode);
        Assert.Equal("gui", DaemonEditorModeValues.Gui);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("batchmode", true, DaemonEditorMode.Batchmode)]
    [InlineData(" gui ", true, DaemonEditorMode.Gui)]
    [InlineData("BATCHMODE", false, DaemonEditorMode.Batchmode)]
    [InlineData("unsupported", false, DaemonEditorMode.Batchmode)]
    [InlineData("", false, DaemonEditorMode.Batchmode)]
    [InlineData(" ", false, DaemonEditorMode.Batchmode)]
    [InlineData(null, false, DaemonEditorMode.Batchmode)]
    public void DaemonEditorModeCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        DaemonEditorMode expectedValue)
    {
        var result = DaemonEditorModeCodec.TryParse(value, out var editorMode);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedValue, editorMode);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonEditorMode.Batchmode, DaemonEditorModeValues.Batchmode)]
    [InlineData(DaemonEditorMode.Gui, DaemonEditorModeValues.Gui)]
    public void DaemonEditorModeCodec_ToValue_ReturnsCanonicalLiteral (
        DaemonEditorMode editorMode,
        string expectedValue)
    {
        Assert.Equal(expectedValue, DaemonEditorModeCodec.ToValue(editorMode));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DaemonSessionOwnerKindValues_HasStableStringValues ()
    {
        Assert.Equal("cli", DaemonSessionOwnerKindValues.Cli);
        Assert.Equal("user", DaemonSessionOwnerKindValues.User);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("cli", true, DaemonSessionOwnerKind.Cli)]
    [InlineData(" user ", true, DaemonSessionOwnerKind.User)]
    [InlineData("CLI", false, DaemonSessionOwnerKind.Cli)]
    [InlineData("supervisor", false, DaemonSessionOwnerKind.Cli)]
    [InlineData("", false, DaemonSessionOwnerKind.Cli)]
    [InlineData(" ", false, DaemonSessionOwnerKind.Cli)]
    [InlineData(null, false, DaemonSessionOwnerKind.Cli)]
    public void DaemonSessionOwnerKindCodec_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        DaemonSessionOwnerKind expectedValue)
    {
        var result = DaemonSessionOwnerKindCodec.TryParse(value, out var ownerKind);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedValue, ownerKind);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonSessionOwnerKind.Cli, DaemonSessionOwnerKindValues.Cli)]
    [InlineData(DaemonSessionOwnerKind.User, DaemonSessionOwnerKindValues.User)]
    public void DaemonSessionOwnerKindCodec_ToValue_ReturnsCanonicalLiteral (
        DaemonSessionOwnerKind ownerKind,
        string expectedValue)
    {
        Assert.Equal(expectedValue, DaemonSessionOwnerKindCodec.ToValue(ownerKind));
    }
}
