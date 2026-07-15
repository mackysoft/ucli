using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceUnityProgressValidationTests
{
    public static TheoryData<string, object> InvalidProgressFrames => new()
    {
        {
            "mismatched-profile-digest",
            CreateProgressFrame(
                BuildRunProgressEventNames.ReadinessCompleted,
                RunId,
                Sha256Digest.Parse(new string('f', 64)),
                BuildRunProgressPhase.Readiness)
        },
        {
            "mismatched-event-phase",
            CreateProgressFrame(
                BuildRunProgressEventNames.ReadinessCompleted,
                RunId,
                ResolveProfileDigest(),
                BuildRunProgressPhase.RunnerInvocation)
        },
        {
            "missing-log-timestamp",
            CreateLogFrame(default, "build log entry")
        },
        {
            "non-utc-log-timestamp",
            CreateLogFrame(new DateTimeOffset(2026, 6, 12, 9, 0, 0, TimeSpan.FromHours(9)), "build log entry")
        },
        {
            "oversized-log-message",
            CreateLogFrame(DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"), new string('x', BuildLogEntryLimits.MaxMessageUtf8Bytes + 1))
        },
    };

    [Theory]
    [Trait("Size", "Medium")]
    [MemberData(nameof(InvalidProgressFrames))]
    public async Task Execute_WithInvalidUnityProgressFrame_ReturnsRunnerInvocationFailedAndDiagnostic (
        string caseName,
        object invalidProgressFrame)
    {
        _ = caseName;
        using var tempDirectory = CreateArtifactDirectoryScope();

        await AssertInvalidUnityProgressFrameReturnsRunnerInvocationFailedAsync(
            tempDirectory.FullPath,
            Assert.IsType<UnityRequestProgressFrame>(invalidProgressFrame));
    }

    private static UnityRequestProgressFrame CreateProgressFrame (
        string eventName,
        Guid runId,
        Sha256Digest profileDigest,
        BuildRunProgressPhase phase)
    {
        return new UnityRequestProgressFrame(
            eventName,
            IpcPayloadCodec.SerializeToElement(new BuildProgressEntry(
                RunId: runId,
                ProfileDigest: profileDigest,
                Phase: phase,
                RunnerKind: null,
                RunnerStatus: null,
                Verdict: null,
                ReportRefs: [],
                ErrorCode: null)));
    }

    private static UnityRequestProgressFrame CreateLogFrame (
        DateTimeOffset timestampUtc,
        string message)
    {
        return new UnityRequestProgressFrame(
            BuildRunProgressEventNames.LogEntry,
            IpcPayloadCodec.SerializeToElement(new
            {
                runId = RunId,
                timestampUtc,
                level = "info",
                message,
                cursor = (string?)null,
                source = "unityLog",
            }));
    }
}
