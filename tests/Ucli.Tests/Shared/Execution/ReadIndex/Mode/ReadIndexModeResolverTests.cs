using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Execution.ReadIndex;

public sealed class ReadIndexModeResolverTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("RequireFresh")]
    [InlineData("AllowStale")]
    [InlineData("Disabled")]
    public void Resolve_WithoutOption_MapsModeFromConfigDefaults (
        string defaultModeName)
    {
        Assert.True(Enum.TryParse<ReadIndexMode>(defaultModeName, out var defaultMode));
        var config = CreateConfig(defaultMode);

        var result = ReadIndexModeResolver.Resolve(optionValue: (string?)null, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(defaultMode, result.Mode);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("disabled", "Disabled")]
    [InlineData("allowStale", "AllowStale")]
    [InlineData("requireFresh", "RequireFresh")]
    [InlineData("DISABLED", "Disabled")]
    public void Resolve_WithOption_UsesOptionValue (
        string optionValue,
        string expectedModeName)
    {
        var config = CreateConfig(ReadIndexMode.RequireFresh);

        var result = ReadIndexModeResolver.Resolve(optionValue, config);

        Assert.True(result.IsSuccess);
        Assert.True(Enum.TryParse<ReadIndexMode>(expectedModeName, out var expectedMode));
        Assert.Equal(expectedMode, result.Mode);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("invalidMode")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Resolve_WithInvalidOption_ReturnsInvalidArgument (
        string optionValue)
    {
        var config = CreateConfig(ReadIndexMode.RequireFresh);

        var result = ReadIndexModeResolver.Resolve(optionValue, config);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Mode);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("readIndexMode", error.Message, StringComparison.Ordinal);
    }

    private static UcliConfig CreateConfig (ReadIndexMode defaultMode)
    {
        return new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: defaultMode,
            OperationAllowlist:
            [
                UcliContractConstants.Config.DefaultOperationAllowlistPattern,
            ]);
    }
}