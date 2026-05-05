using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

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

        var result = ReadIndexModeResolver.Resolve(optionValue: (ReadIndexMode?)null, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(defaultMode, result.Mode);
        Assert.Null(result.Error);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(ReadIndexMode.Disabled)]
    [InlineData(ReadIndexMode.AllowStale)]
    [InlineData(ReadIndexMode.RequireFresh)]
    public void Resolve_WithOption_UsesOptionValue (
        ReadIndexMode optionValue)
    {
        var config = CreateConfig(ReadIndexMode.RequireFresh);

        var result = ReadIndexModeResolver.Resolve(optionValue, config);

        Assert.True(result.IsSuccess);
        Assert.Equal(optionValue, result.Mode);
        Assert.Null(result.Error);
    }

    private static UcliConfig CreateConfig (ReadIndexMode defaultMode)
    {
        return new UcliConfig(
            SchemaVersion: 1,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: defaultMode,
            OperationAllowlist:
            [
                "^ucli\\.",
            ]);
    }
}
