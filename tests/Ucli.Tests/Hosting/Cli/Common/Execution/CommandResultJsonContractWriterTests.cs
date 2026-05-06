using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

public sealed class CommandResultJsonContractWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Write_WritesFixedEnvelopeJson ()
    {
        var result = new CommandResult(
            ProtocolVersion: 1,
            Command: UcliCommandNames.Status,
            Status: "error",
            ExitCode: (int)CliExitCode.InvalidArgument,
            Message: "Failed.",
            Payload: new { sampleValue = true },
            Errors:
            [
                new CommandError("INVALID_ARGUMENT", "Failed.", null),
            ]);

        var json = new CommandResultJsonContractWriter().Write(result);

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "protocolVersion": 1,
                  "command": "status",
                  "status": "error",
                  "exitCode": 3,
                  "message": "Failed.",
                  "payload": {
                    "sampleValue": true
                  },
                  "errors": [
                    {
                      "code": "INVALID_ARGUMENT",
                      "message": "Failed.",
                      "opId": null
                    }
                  ]
                }
                """),
            json);
    }

    private static string ExpectedJson (string json)
    {
        return json
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            + "\n";
    }

    private static void AssertExactJson (
        string expected,
        string actual)
    {
        Assert.DoesNotContain("\r", actual);
        Assert.EndsWith("\n", actual);
        Assert.Equal(expected, actual);
    }
}
