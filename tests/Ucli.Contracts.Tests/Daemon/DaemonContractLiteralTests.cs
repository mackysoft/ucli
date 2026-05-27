using MackySoft.Ucli.Contracts.Daemon;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Daemon;

public sealed class DaemonContractLiteralTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("batchmode", true, DaemonEditorMode.Batchmode)]
    [InlineData(" gui ", true, DaemonEditorMode.Gui)]
    [InlineData("BATCHMODE", false, DaemonEditorMode.Batchmode)]
    [InlineData("unsupported", false, DaemonEditorMode.Batchmode)]
    [InlineData("", false, DaemonEditorMode.Batchmode)]
    [InlineData(" ", false, DaemonEditorMode.Batchmode)]
    [InlineData(null, false, DaemonEditorMode.Batchmode)]
    public void DaemonEditorModeContractLiteral_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        DaemonEditorMode expectedValue)
    {
        var result = ContractLiteralInputParser.TryParseTrimmed<DaemonEditorMode>(value, out var editorMode);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedValue, editorMode);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonEditorMode.Batchmode, "batchmode")]
    [InlineData(DaemonEditorMode.Gui, "gui")]
    public void DaemonEditorModeContractLiteral_ToValue_ReturnsCanonicalLiteral (
        DaemonEditorMode editorMode,
        string expectedValue)
    {
        Assert.Equal(expectedValue, ContractLiteralCodec.ToValue(editorMode));
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
    public void DaemonSessionOwnerKindContractLiteral_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        DaemonSessionOwnerKind expectedValue)
    {
        var result = ContractLiteralInputParser.TryParseTrimmed<DaemonSessionOwnerKind>(value, out var ownerKind);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedValue, ownerKind);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonSessionOwnerKind.Cli, "cli")]
    [InlineData(DaemonSessionOwnerKind.User, "user")]
    public void DaemonSessionOwnerKindContractLiteral_ToValue_ReturnsCanonicalLiteral (
        DaemonSessionOwnerKind ownerKind,
        string expectedValue)
    {
        Assert.Equal(expectedValue, ContractLiteralCodec.ToValue(ownerKind));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("auto", true, DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData(" keep ", true, DaemonStartupBlockedProcessPolicy.Keep)]
    [InlineData("terminate", true, DaemonStartupBlockedProcessPolicy.Terminate)]
    [InlineData("AUTO", false, DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData("unsupported", false, DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData("", false, DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData(" ", false, DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData(null, false, DaemonStartupBlockedProcessPolicy.Auto)]
    public void DaemonStartupBlockedProcessPolicyContractLiteral_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        DaemonStartupBlockedProcessPolicy expectedValue)
    {
        var result = ContractLiteralInputParser.TryParseTrimmed<DaemonStartupBlockedProcessPolicy>(value, out var policy);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedValue, policy);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DaemonStartupBlockedProcessPolicy.Auto, "auto")]
    [InlineData(DaemonStartupBlockedProcessPolicy.Keep, "keep")]
    [InlineData(DaemonStartupBlockedProcessPolicy.Terminate, "terminate")]
    public void DaemonStartupBlockedProcessPolicyContractLiteral_ToValue_ReturnsCanonicalLiteral (
        DaemonStartupBlockedProcessPolicy policy,
        string expectedValue)
    {
        Assert.Equal(expectedValue, ContractLiteralCodec.ToValue(policy));
    }
}
