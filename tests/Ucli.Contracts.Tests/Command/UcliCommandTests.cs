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
}
