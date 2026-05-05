using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Execution.Process;

namespace MackySoft.Ucli.Tests.Execution.Timeout;

public sealed class IpcCommandTimeoutResolverTests
{
    private static readonly UcliCommand Command = new("test");

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithoutOption_UsesConfigDefault ()
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3200);

        var result = IpcCommandTimeoutResolver.ResolveNormalized(null, Command, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(3200), result.Timeout);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithOption_UsesOptionValue ()
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3000);

        var result = IpcCommandTimeoutResolver.ResolveNormalized(4500, Command, config);

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
                [Command.Name] = 6200,
            });

        var result = IpcCommandTimeoutResolver.ResolveNormalized(null, Command, config);

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
                [Command.Name] = null,
            });

        var result = IpcCommandTimeoutResolver.ResolveNormalized(null, Command, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(3000), result.Timeout);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_WithInvalidNormalizedOption_ReturnsInvalidArgument (int optionValue)
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3000);

        var result = IpcCommandTimeoutResolver.ResolveNormalized(optionValue, Command, config);

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

        var result = IpcCommandTimeoutResolver.ResolveNormalized(null, Command, config);

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
                [Command.Name] = 0,
            });

        var result = IpcCommandTimeoutResolver.ResolveNormalized(null, Command, config);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Timeout);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ipcTimeoutMillisecondsByCommand", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithInvalidCommand_ThrowsArgumentException ()
    {
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3000);

        Assert.Throws<ArgumentException>(() =>
        {
            _ = IpcCommandTimeoutResolver.ResolveNormalized(null, default, config);
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
