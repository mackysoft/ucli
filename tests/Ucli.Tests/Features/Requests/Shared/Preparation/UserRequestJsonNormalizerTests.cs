using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Tests;

public sealed class UserRequestJsonNormalizerTests
{
    private const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenUserRequestContainsOnlySteps_AddsProtocolVersionAndRequestId ()
    {
        var normalizer = CreateNormalizer();

        var result = normalizer.Normalize("""{"steps":[]}""");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.RequestJson);
        using var document = JsonDocument.Parse(result.RequestJson!);
        var root = document.RootElement;
        Assert.Equal(IpcProtocol.CurrentVersion, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(RequestId, root.GetProperty("requestId").GetString());
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
                  "id": "step-1",
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
        Assert.Equal("step-1", step.GetProperty("id").GetString());
        Assert.Equal("ucli.go.describe", step.GetProperty("op").GetString());
        Assert.Equal("Root", step.GetProperty("args").GetProperty("path").GetString());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("protocolVersion")]
    [InlineData("requestId")]
    public void Normalize_WhenUserRequestContainsReservedRootProperty_ReturnsInvalidArgument (string propertyName)
    {
        var normalizer = CreateNormalizer();
        var requestJson = $$"""{"{{propertyName}}":"value","steps":[]}""";

        var result = normalizer.Normalize(requestJson);

        Assert.False(result.IsSuccess);
        Assert.Null(result.RequestJson);
        Assert.NotNull(result.Error);
        Assert.Contains("reserved", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains(propertyName, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenUserRequestContainsUnknownRootProperty_ReturnsInvalidArgument ()
    {
        var normalizer = CreateNormalizer();

        var result = normalizer.Normalize("""{"steps":[],"unknown":true}""");

        Assert.False(result.IsSuccess);
        Assert.Null(result.RequestJson);
        Assert.NotNull(result.Error);
        Assert.Contains("unknown property", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("unknown", result.Error.Message, StringComparison.Ordinal);
    }

    private static UserRequestJsonNormalizer CreateNormalizer ()
    {
        return new UserRequestJsonNormalizer(new FixedRequestIdFactory(RequestId));
    }

    private sealed class FixedRequestIdFactory : IRequestIdFactory
    {
        private readonly string requestId;

        public FixedRequestIdFactory (string requestId)
        {
            this.requestId = requestId;
        }

        public string Create ()
        {
            return requestId;
        }
    }
}
