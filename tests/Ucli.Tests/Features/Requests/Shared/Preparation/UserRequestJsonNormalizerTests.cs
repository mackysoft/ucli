using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests;

public sealed class UserRequestJsonNormalizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenUserRequestContainsOnlySteps_AddsProtocolVersion ()
    {
        var normalizer = CreateNormalizer();

        var result = normalizer.Normalize("""{"steps":[]}""");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.RequestJson);
        using var document = JsonDocument.Parse(result.RequestJson!);
        var root = document.RootElement;
        Assert.Equal(IpcProtocol.CurrentVersion, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("steps").ValueKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_PreservesStepsContent ()
    {
        var normalizer = CreateNormalizer();

        var result = normalizer.Normalize(
            """
            {
              "steps": [
                {
                  "kind": "op",
                  "op": "ucli.go.describe",
                  "args": {
                    "path": "Root"
                  }
                }
              ]
            }
            """);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.RequestJson);
        using var document = JsonDocument.Parse(result.RequestJson!);
        var step = document.RootElement.GetProperty("steps")[0];
        Assert.Equal("op", step.GetProperty("kind").GetString());
        Assert.Equal("ucli.go.describe", step.GetProperty("op").GetString());
        Assert.Equal("Root", step.GetProperty("args").GetProperty("path").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenUserRequestContainsProtocolVersion_ReturnsReservedPropertyError ()
    {
        var normalizer = CreateNormalizer();

        var result = normalizer.Normalize("""{"protocolVersion":1,"steps":[]}""");

        Assert.False(result.IsSuccess);
        Assert.Null(result.RequestJson);
        Assert.NotNull(result.Error);
        Assert.Contains("reserved", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("protocolVersion", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("unknown")]
    public void Normalize_WhenUserRequestContainsUnknownRootProperty_ReturnsInvalidArgument (string propertyName)
    {
        var normalizer = CreateNormalizer();
        var requestJson = $$"""{"steps":[],"{{propertyName}}":true}""";

        var result = normalizer.Normalize(requestJson);

        Assert.False(result.IsSuccess);
        Assert.Null(result.RequestJson);
        Assert.NotNull(result.Error);
        Assert.Contains(propertyName, result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenUserRequestRootIsNotObject_ReturnsInvalidArgument ()
    {
        var normalizer = CreateNormalizer();

        var result = normalizer.Normalize("""[]""");

        Assert.False(result.IsSuccess);
        Assert.Null(result.RequestJson);
        Assert.NotNull(result.Error);
        Assert.Contains("root must be an object", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenStepsPropertyIsMissing_ReturnsInvalidArgument ()
    {
        var normalizer = CreateNormalizer();

        var result = normalizer.Normalize("""{}""");

        Assert.False(result.IsSuccess);
        Assert.Null(result.RequestJson);
        Assert.NotNull(result.Error);
        Assert.Contains("steps", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("required", result.Error.Message, StringComparison.Ordinal);
    }

    private static UserRequestJsonNormalizer CreateNormalizer ()
    {
        return new UserRequestJsonNormalizer();
    }
}
