using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal static class ValidateServiceTestSupport
{
    public static PreparedRequestContext CreatePreparedRequestContext (UcliConfig? config = null)
    {
        return new PreparedRequestContext(
            RequestJson: """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""",
            Request: new ValidateRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Steps: Array.Empty<ValidateRequestStep?>()),
            ProjectContext: ProjectContextTestFactory.CreateTemporaryFixtureProject(config));
    }

    public static UcliConfig CreateConfigWithValidateTimeout (int? timeoutMilliseconds)
    {
        var timeoutOverrides = new Dictionary<string, int?>(UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal)
        {
            [UcliCommandIds.Validate.Name] = timeoutMilliseconds,
        };

        return UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = timeoutOverrides,
        };
    }

    public static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        IndexFreshness freshness)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoSource.Index,
            Freshness: freshness,
            GeneratedAtUtc: used
                ? DateTimeOffset.Parse("2026-03-06T00:00:00+00:00")
                : null,
            FallbackReason: used
                ? null
                : "readIndex disabled by mode.");
    }

    public static RecordingRequestPreparationService CreateRequestPreparationService (RequestPreparationResult prepareResult)
    {
        ArgumentNullException.ThrowIfNull(prepareResult);
        return new RecordingRequestPreparationService
        {
            PrepareResult = prepareResult,
            ParseResult = CreateParsedRequestResult(prepareResult),
        };
    }

    private static ParsedRequestResult CreateParsedRequestResult (RequestPreparationResult prepareResult)
    {
        if (!prepareResult.IsSuccess)
        {
            return ParsedRequestResult.Failure(prepareResult.Error!);
        }

        var preparedRequest = prepareResult.PreparedRequest!;
        return ParsedRequestResult.Success(new ParsedRequestContext(
            preparedRequest.RequestJson,
            preparedRequest.Request));
    }
}
