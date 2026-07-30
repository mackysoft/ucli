namespace MackySoft.Ucli.Application.Tests.Execution.Results;

internal static class RequestServiceResultInvariantTestSupport
{
    public static readonly Guid RequestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");

    public static IReadOnlyList<ApplicationFailure> CreateErrors ()
    {
        return
        [
            ApplicationFailure.InternalError(
                "Failure message.",
                UcliCoreErrorCodes.InternalError,
                instancePath: null,
                startupFailure: null),
        ];
    }

    public static ReadIndexInfo CreateReadIndexInfo ()
    {
        return new ReadIndexInfo(
            Used: true,
            Hit: true,
            Source: ReadIndexInfoSource.Index,
            Freshness: IndexFreshness.Fresh,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
            FallbackReason: null);
    }
}
