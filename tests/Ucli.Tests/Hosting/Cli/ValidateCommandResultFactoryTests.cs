using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenSuccess_ReturnsOkEnvelopeWithReadIndexPayload ()
    {
        var result = ValidateCommandResultFactory.Create(ValidateServiceResult.Success(
            new ValidateExecutionOutput(CreateReadIndexInfo()),
            "Static validation passed."));

        Assert.Equal(UcliCommandNames.Validate, result.Command);
        Assert.Equal("ok", result.Status);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .HasProperty("readIndex", readIndex => readIndex
                .HasBoolean("Used", true)
                .HasBoolean("Hit", true)
                .HasString("Source", "index")
                .HasString("Freshness", "probable"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenValidationFails_ReturnsInvalidArgumentEnvelope ()
    {
        var result = ValidateCommandResultFactory.Create(ValidateServiceResult.ValidationFailure(
            new ValidateExecutionOutput(CreateReadIndexInfo()),
            "Static validation failed.",
            [
                new ValidationError(
                    ValidationErrorCodes.OperationArgsInvalid,
                    "Operation args are invalid.",
                    "step-1"),
            ]));

        Assert.Equal(UcliCommandNames.Validate, result.Command);
        Assert.Equal("error", result.Status);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, result.Errors[0].Code);
        Assert.Equal("step-1", result.Errors[0].OpId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenInfraFailureOccurs_ReturnsToolErrorEnvelope ()
    {
        var result = ValidateCommandResultFactory.Create(ValidateServiceResult.Failure(
            "Index contract file 'ops.catalog.json' is malformed.",
            IpcErrorCodes.ReadIndexFormatInvalid,
            new ValidateExecutionOutput(CreateReadIndexInfo())));

        Assert.Equal(UcliCommandNames.Validate, result.Command);
        Assert.Equal("error", result.Status);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.Errors[0].Code);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .HasProperty("readIndex", readIndex => readIndex
                .HasString("Source", "index")
                .HasString("Freshness", "probable"));
    }

    private static ReadIndexInfo CreateReadIndexInfo ()
    {
        return new ReadIndexInfo(
            Used: true,
            Hit: true,
            Source: ReadIndexInfoTextCodec.SourceIndex,
            Freshness: ReadIndexInfoTextCodec.FreshnessProbable,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            FallbackReason: null);
    }
}