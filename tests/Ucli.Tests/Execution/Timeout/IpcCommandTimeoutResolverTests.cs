using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Tests.Execution.Timeout;

public sealed class IpcCommandTimeoutResolverTests
{
    private const string CommandName = "status";

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithoutOption_UsesConfigDefault ()
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3200);

        var result = IpcCommandTimeoutResolver.Resolve(null, CommandName, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(3200), result.Timeout);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithOption_UsesOptionValue ()
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3000);

        var result = IpcCommandTimeoutResolver.Resolve("4500", CommandName, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), result.Timeout);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithoutOption_UsesCommandOverride ()
    {
        var config = CreateConfig(
            ipcDefaultTimeoutMilliseconds: 3000,
            ipcTimeoutMillisecondsByCommand: new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [CommandName] = 6200,
            });

        var result = IpcCommandTimeoutResolver.Resolve(null, CommandName, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(6200), result.Timeout);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithoutOption_UsesConfigDefaultWhenCommandOverrideIsNull ()
    {
        var config = CreateConfig(
            ipcDefaultTimeoutMilliseconds: 3000,
            ipcTimeoutMillisecondsByCommand: new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [CommandName] = null,
            });

        var result = IpcCommandTimeoutResolver.Resolve(null, CommandName, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(3000), result.Timeout);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public void Resolve_WithInvalidOption_ReturnsInvalidArgument (string optionValue)
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3000);

        var result = IpcCommandTimeoutResolver.Resolve(optionValue, CommandName, config);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Timeout);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithInvalidConfigDefault_ReturnsInvalidArgument ()
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 0);

        var result = IpcCommandTimeoutResolver.Resolve(null, CommandName, config);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Timeout);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("config ipcDefaultTimeoutMilliseconds", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithInvalidCommandOverride_ReturnsInvalidArgument ()
    {
        var config = CreateConfig(
            ipcDefaultTimeoutMilliseconds: 3000,
            ipcTimeoutMillisecondsByCommand: new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [CommandName] = 0,
            });

        var result = IpcCommandTimeoutResolver.Resolve(null, CommandName, config);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Timeout);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ipcTimeoutMillisecondsByCommand", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithInvalidCommandName_ThrowsArgumentException ()
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3000);

        Assert.Throws<ArgumentException>(() =>
        {
            _ = IpcCommandTimeoutResolver.Resolve(null, " ", config);
        });
    }

    private static UcliConfig CreateConfig (
        int ipcDefaultTimeoutMilliseconds,
        IReadOnlyDictionary<string, int?>? ipcTimeoutMillisecondsByCommand = null)
    {
        return new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                UcliContractConstants.Config.DefaultOperationAllowlistPattern,
            ])
        {
            IpcDefaultTimeoutMilliseconds = ipcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand = ipcTimeoutMillisecondsByCommand
                ?? new Dictionary<string, int?>(StringComparer.Ordinal),
        };
    }
}