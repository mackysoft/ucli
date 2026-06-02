using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Command;

public sealed class UcliCommandTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("status")]
    [InlineData("daemon.cleanup")]
    [InlineData("daemon.status")]
    [InlineData("daemon.list")]
    [InlineData("test.run")]
    [InlineData("logs.daemon.read")]
    [InlineData("logs.unity.read")]
    [InlineData("logs.unity.clear")]
    public void Constructor_WithValidName_CreatesCommand (string name)
    {
        var command = new UcliCommand(name);

        Assert.Equal(name, command.Name);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("status")]
    [InlineData("daemon.cleanup")]
    [InlineData("daemon.status")]
    [InlineData("daemon.list")]
    [InlineData("test.run")]
    [InlineData("logs.daemon.read")]
    [InlineData("logs.unity.read")]
    [InlineData("logs.unity.clear")]
    public void IsValidName_WithValidName_ReturnsTrue (string name)
    {
        Assert.True(UcliCommand.IsValidName(name));
        Assert.True(new UcliCommand(name).IsValid);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("daemon status")]
    [InlineData("daemon\tstatus")]
    [InlineData(".daemon")]
    [InlineData("daemon.")]
    [InlineData("daemon..status")]
    public void IsValidName_WithInvalidName_ReturnsFalse (string? name)
    {
        Assert.False(UcliCommand.IsValidName(name));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValid_OnDefaultCommand_ReturnsFalse ()
    {
        Assert.False(default(UcliCommand).IsValid);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliCommandIds_ExposePlayModeCommandLiterals ()
    {
        Assert.Equal("play", UcliCommandIds.Play.Name);
        Assert.Equal("play.status", UcliCommandIds.PlayStatus.Name);
        Assert.Equal("play.enter", UcliCommandIds.PlayEnter.Name);
        Assert.Equal("play.exit", UcliCommandIds.PlayExit.Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PublicCommandCatalog_IncludesPlayModeCommandFamily ()
    {
        Assert.Contains(UcliCommandIds.Play, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.PlayStatus, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.PlayEnter, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.PlayExit, UcliPublicCommandCatalog.KnownCommands);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("daemon status")]
    [InlineData("daemon\tstatus")]
    [InlineData(".daemon")]
    [InlineData("daemon.")]
    [InlineData("daemon..status")]
    public void Constructor_WithInvalidName_ThrowsArgumentException (string name)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new UcliCommand(name);
        });
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("succeeded", true, CommandProgressResult.Succeeded)]
    [InlineData(" failed ", true, CommandProgressResult.Failed)]
    [InlineData("SUCCEEDED", false, CommandProgressResult.Succeeded)]
    [InlineData("unsupported", false, CommandProgressResult.Succeeded)]
    [InlineData("", false, CommandProgressResult.Succeeded)]
    [InlineData(" ", false, CommandProgressResult.Succeeded)]
    [InlineData(null, false, CommandProgressResult.Succeeded)]
    public void CommandProgressResultContractLiteral_TryParse_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        CommandProgressResult expectedValue)
    {
        var result = ContractLiteralInputParser.TryParseTrimmed<CommandProgressResult>(value, out var progressResult);

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedValue, progressResult);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(CommandProgressResult.Succeeded, "succeeded")]
    [InlineData(CommandProgressResult.Failed, "failed")]
    public void CommandProgressResultContractLiteral_ToValue_ReturnsCanonicalLiteral (
        CommandProgressResult progressResult,
        string expectedValue)
    {
        Assert.Equal(expectedValue, ContractLiteralCodec.ToValue(progressResult));
    }
}
