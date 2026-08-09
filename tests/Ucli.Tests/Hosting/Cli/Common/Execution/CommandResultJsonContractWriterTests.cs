using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using static MackySoft.Tests.JsonTextNormalization;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

public sealed class CommandResultJsonContractWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Write_WritesFixedEnvelopeJson ()
    {
        var result = CommandResult.InvalidArgument(
            UcliCommandNames.Status,
            "Failed.",
            UcliCoreErrorCodes.InvalidArgument,
            new { sampleValue = true });

        var json = new CommandResultJsonContractWriter().Write(result);

        GoldenJsonAssert.Equal(
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
                      "instancePath": null
                    }
                  ]
                }
                """),
            json);
    }

}
