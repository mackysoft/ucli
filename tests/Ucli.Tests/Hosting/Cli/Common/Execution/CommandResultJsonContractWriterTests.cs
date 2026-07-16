using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using static MackySoft.Tests.JsonTextAssert;

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
            Status: CommandResultStatus.Error,
            ExitCode: (int)CliExitCode.InvalidArgument,
            Message: "Failed.",
            Payload: new { sampleValue = true },
            Errors:
            [
                new CommandError(UcliCoreErrorCodes.InvalidArgument, "Failed.", null),
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

}
