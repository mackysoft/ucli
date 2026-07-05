using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ValidateCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_MapsOptionsAndRequestJsonToServiceInput ()
    {
        var service = new RecordingValidateService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new ValidateCommand(service, RequestInputReaderStub.Success(DefaultRequestJson), CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ValidateAsync(
            projectPath: "/repo/UnityProject",
            timeout: "1234",
            readIndexMode: "disabled",
            cancellationToken: CancellationToken.None));

        ValidateCommandAssert.SucceededWithDispatchedRequest(
            result,
            service,
            CancellationToken.None,
            "/repo/UnityProject",
            expectedTimeoutMilliseconds: 1234,
            ReadIndexMode.Disabled,
            expectedRequestJson: DefaultRequestJson);
    }
}
