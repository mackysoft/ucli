using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal static class ValidateServiceTestSupport
{
    public static PreparedRequestContext CreatePreparedRequestContext (UcliConfig? config = null)
    {
        return new PreparedRequestContext(
            requestJson: """{"protocolVersion":1,"steps":[]}""",
            request: new ValidateRequest(
                ProtocolVersion: 1,
                Steps: Array.Empty<ValidateRequestStep?>()),
            projectContext: ProjectContextTestFactory.CreateTemporaryFixtureProject(config));
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
        };
    }
}
