using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ValidateRequestJsonParserOperationStepContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationArgsPropertyIsMissing_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateOperationRequest(
            $$"""
            "kind": "op",
            "id": "step-1",
            "op": "{{UcliPrimitiveOperationNames.SceneOpen}}"
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "args");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationArgsPropertyIsNotObject_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateOperationRequest(
            $$"""
            "kind": "op",
            "id": "step-1",
            "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
            "args": []
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "args");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationContainsUnknownProperty_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateValidOperationRequest(
            """
            ,
            "unknown": 1
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "unknown");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationIdContainsOuterWhitespace_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateOperationRequest(
            $$"""
            "kind": "op",
            "id": " step-1 ",
            "op": "{{UcliPrimitiveOperationNames.SceneOpen}}",
            "args": {
              "path": "{{ValidateRequestJsonParserTestSupport.ScenePath}}"
            }
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "id");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationNameContainsOuterWhitespace_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateOperationRequest(
            $$"""
            "kind": "op",
            "id": "step-1",
            "op": " {{UcliPrimitiveOperationNames.SceneOpen}} ",
            "args": {
              "path": "{{ValidateRequestJsonParserTestSupport.ScenePath}}"
            }
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "op");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationAliasContractIsInvalid_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateValidOperationRequest(
            """
            ,
            "as": 123
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "as");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenOperationExpectationContractIsInvalid_ReturnsInvalidArgument ()
    {
        var requestJson = ValidateRequestJsonParserTestSupport.CreateValidOperationRequest(
            """
            ,
            "expect": {
              "count": 1,
              "min": 0
            }
            """);

        ValidateRequestJsonParserTestSupport.AssertInvalidArgument(requestJson, "expect");
    }
}
