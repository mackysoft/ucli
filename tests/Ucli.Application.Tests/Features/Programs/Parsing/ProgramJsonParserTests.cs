using MackySoft.Ucli.Application.Features.Programs.Parsing;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Parsing;

public sealed class ProgramJsonParserTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithInlineCallStep_ReturnsTypedProgram ()
    {
        const string json = """
        {
          "steps": [
            {
              "command": "call",
              "timeoutMilliseconds": 1000,
              "steps": [
                {
                  "kind": "op",
                  "op": "ucli.scene.open",
                  "args": { "path": "Assets/Main.unity" }
                }
              ]
            }
          ]
        }
        """;

        var result = new ProgramJsonParser().Parse(json);

        Assert.True(result.IsSuccess);
        var program = Assert.IsType<ProgramDefinition>(result.Program);
        var call = Assert.IsType<InlineCallProgramStep>(Assert.Single(program.Steps));
        Assert.Equal(1000, call.TimeoutMilliseconds);
        Assert.NotNull(call.Request);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithUnknownOrDuplicateStepProperty_ReturnsPointerDiagnostics ()
    {
        const string json = """
        {
          "steps": [
            { "command": "ready", "command": "ready", "extra": true }
          ]
        }
        """;

        var result = new ProgramJsonParser().Parse(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "program.duplicateProperty" && diagnostic.InstancePath == "/steps/0/command");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "program.unknownProperty" && diagnostic.InstancePath == "/steps/0/extra");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithIncompleteScreenshotDimensions_ReturnsExclusiveInputDiagnostic ()
    {
        const string json = """
        { "steps": [{ "command": "screenshot.game", "width": 1280 }] }
        """;

        var result = new ProgramJsonParser().Parse(json);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("program.exclusiveProperty", diagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithInvalidUtf8_ReturnsInvalidJsonDiagnostic ()
    {
        var result = new ProgramJsonParser().Parse([(byte)'{', 0xff, (byte)'}']);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("program.invalidJson", diagnostic.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("0")]
    [InlineData("2147483648")]
    [InlineData("null")]
    public void Parse_WithInvalidExplicitTimeout_RejectsValue (string timeout)
    {
        var result = new ProgramJsonParser().Parse($$"""
        { "steps": [{ "command": "ready", "timeoutMilliseconds": {{timeout}} }] }
        """);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.InstancePath == "/steps/0/timeoutMilliseconds");
    }
}
