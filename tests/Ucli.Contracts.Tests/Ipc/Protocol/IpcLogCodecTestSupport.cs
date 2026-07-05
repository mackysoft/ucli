namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

internal static class IpcLogCodecTestSupport
{
    public static TheoryData<int?, string?, string?, string> WindowBoundsFailureCases => new()
    {
        { 0, null, null, "tail must be greater than zero. Actual: 0." },
        { -1, null, null, "tail must be greater than zero. Actual: -1." },
        { null, "invalid", null, "since must be an ISO 8601 timestamp with timezone offset. Actual: invalid." },
        { null, null, "invalid", "until must be an ISO 8601 timestamp with timezone offset. Actual: invalid." },
        {
            null,
            "2026-03-05T10:36:22.0000000+09:00",
            "2026-03-05T10:35:22.0000000+09:00",
            "since must be less than or equal to until. since=2026-03-05T10:36:22.0000000+09:00, until=2026-03-05T10:35:22.0000000+09:00."
        },
    };
}
