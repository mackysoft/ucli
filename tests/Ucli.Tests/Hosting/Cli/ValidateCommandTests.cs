using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_UsesValidateServiceAndWritesCommandResult ()
    {
        var service = new StubValidateService((input, _) => ValueTask.FromResult(ValidateServiceResult.Success(
            new ValidateExecutionOutput(new ReadIndexInfo(
                Used: false,
                Hit: false,
                Source: ReadIndexInfoTextCodec.SourceIndex,
                Freshness: ReadIndexInfoTextCodec.FreshnessProbable,
                GeneratedAtUtc: null,
                FallbackReason: "readIndex disabled by mode.")),
            "Static validation passed.")));
        var command = new ValidateCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Validate(
            requestPath: "/repo/request.json",
            projectPath: "/repo/UnityProject",
            readIndexMode: "disabled",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.NotNull(service.CapturedInput);
        Assert.Equal("/repo/request.json", service.CapturedInput!.RequestPath);
        Assert.Equal("/repo/UnityProject", service.CapturedInput.ProjectPath);
        Assert.Equal(ReadIndexMode.Disabled, service.CapturedInput.ReadIndexMode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Validate,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenReadIndexModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubValidateService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ValidateCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Validate(
            readIndexMode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Validate,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
    }

    private sealed class StubValidateService : IValidateService
    {
        private readonly Func<ValidateCommandInput, CancellationToken, ValueTask<ValidateServiceResult>> handler;

        public StubValidateService (Func<ValidateCommandInput, CancellationToken, ValueTask<ValidateServiceResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public ValidateCommandInput? CapturedInput { get; private set; }

        public ValueTask<ValidateServiceResult> Execute (
            ValidateCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            return handler(input, cancellationToken);
        }
    }
}