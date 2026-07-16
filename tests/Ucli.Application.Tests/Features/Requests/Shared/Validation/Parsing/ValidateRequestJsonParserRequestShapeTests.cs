namespace MackySoft.Ucli.Application.Tests;

public sealed class ValidateRequestJsonParserRequestShapeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenJsonIsMalformed_ReturnsInvalidArgument ()
    {
        ValidateRequestJsonParserTestSupport.AssertInvalidArgument("{", "invalid");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenRequestContainsUnknownTopLevelProperty_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateRequestWithSteps(
            ValidateRequestJsonParserTestSupport.ValidOperationStepsJson,
            """
            ,
              "unknown": 1
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "unknown");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenRequestContainsRequestId_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateRequestWithSteps(
            ValidateRequestJsonParserTestSupport.ValidOperationStepsJson,
            """
            ,
              "requestId": "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "unknown", "requestId");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenStepsPropertyIsNotArray_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateRequestWithSteps("{}");

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "steps");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenProtocolVersionTypeIsInvalid_ReturnsInvalidArgument ()
    {
        const string requestJson = """
            {
              "protocolVersion": "1",
              "steps": []
            }
            """;

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "protocolVersion", "integer");
    }
}
