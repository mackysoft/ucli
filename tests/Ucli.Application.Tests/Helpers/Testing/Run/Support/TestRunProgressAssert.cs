using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Tests;

internal static class TestRunProgressAssert
{
    public static void RunStartedAndUnityProgressForwarded (
        CollectingCommandProgressSink progressSink,
        Guid expectedRunId)
    {
        Assert.Collection(
            progressSink.Entries,
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.RunStarted, entry.EventName);
                var payload = Assert.IsType<TestRunStartedEntry>(entry.Payload);
                Assert.Equal(expectedRunId, payload.RunId);
                Assert.Equal("editmode", payload.TestPlatform);
            },
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.RunStarted, entry.EventName);
                var payload = Assert.IsType<TestRunStartedEntry>(entry.Payload);
                Assert.Equal(expectedRunId, payload.RunId);
                Assert.Equal("MyGame.Tests", Assert.Single(payload.AssemblyNames));
            },
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.CaseStarted, entry.EventName);
                var payload = Assert.IsType<TestCaseStartedEntry>(entry.Payload);
                Assert.Equal(expectedRunId, payload.RunId);
                Assert.Equal("test-id", payload.TestId);
                Assert.Equal("SmokeTest.Passes", payload.TestName);
            },
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.CaseFinished, entry.EventName);
                var payload = Assert.IsType<TestCaseFinishedEntry>(entry.Payload);
                Assert.Equal(expectedRunId, payload.RunId);
                Assert.Equal(TestCaseResult.Pass, payload.Result);
                Assert.Equal(42, payload.DurationMilliseconds);
            },
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.RunDiagnostic, entry.EventName);
                var payload = Assert.IsType<TestRunDiagnosticEntry>(entry.Payload);
                Assert.Equal(expectedRunId, payload.RunId);
                Assert.Equal(new UcliCode("TEST_PROGRESS_STUB"), payload.Code);
            });
    }

    public static void RejectedUnityProgressStoppedAfterRunStarted (CollectingCommandProgressSink progressSink)
    {
        var entry = Assert.Single(progressSink.Entries);
        Assert.Equal(TestRunProgressEventNames.RunStarted, entry.EventName);
    }
}
