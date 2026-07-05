using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

namespace MackySoft.Ucli.Tests;

internal static class ValidateCommandTestData
{
    public const string DefaultRequestJson = """{"steps":[]}""";

    public static ValidateServiceResult CreateSuccessResult ()
    {
        return ValidateServiceResult.Success(
            new ValidateExecutionOutput(
                ProjectIdentityInfoTestFactory.Create(),
                new ReadIndexInfo(
                    Used: false,
                    Hit: false,
                    Source: ReadIndexInfoSource.Index,
                    Freshness: IndexFreshness.Probable,
                    GeneratedAtUtc: null,
                    FallbackReason: "readIndex disabled by mode.")),
            "Static validation passed.");
    }
}
