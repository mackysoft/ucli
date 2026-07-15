using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityLogsReadQueryEngineTests
    {
        [Test]
        [Category("Size.Small")]
        public void Filter_WhenSourceSpecified_ReturnsOnlyMatchingSource ()
        {
            var queryEngine = new UnityLogsReadQueryEngine();

            var filtered = queryEngine.Filter(
                CreateEvents(),
                new UnityLogsReadFilter(
                    AfterSequence: null,
                    Tail: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: IpcLogQueryTarget.Message,
                    Source: IpcUnityLogSource.Compile,
                    StackTraceMode: IpcUnityLogStackTraceMode.All,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            Assert.That(filtered.Count, Is.EqualTo(1));
            Assert.That(filtered[0].Source, Is.EqualTo(IpcUnityLogSource.Compile));
        }

        [Test]
        [Category("Size.Small")]
        public void Filter_WhenQueryTargetIsStackAndStackTraceModeNone_ReturnsNoMatch ()
        {
            var queryEngine = new UnityLogsReadQueryEngine();

            var filtered = queryEngine.Filter(
                CreateEvents(),
                new UnityLogsReadFilter(
                    AfterSequence: null,
                    Tail: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: "SocketException",
                    QueryTarget: IpcLogQueryTarget.Stack,
                    Source: null,
                    StackTraceMode: IpcUnityLogStackTraceMode.None,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            Assert.That(filtered, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void Filter_WhenStackTraceIsTruncated_QueryUsesEffectiveStackTrace ()
        {
            var queryEngine = new UnityLogsReadQueryEngine();
            var events = new List<UnityLogEvent>
            {
                new UnityLogEvent(
                    Sequence: 1,
                    Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 22, TimeSpan.FromHours(9)),
                    Level: IpcLogLevel.Error,
                    Source: IpcUnityLogSource.Runtime,
                    Message: "runtime error",
                    StackTrace: "frame 1\nframe 2\nSocketException: broken pipe",
                    Cursor: "stream-1:1"),
            };

            var filtered = queryEngine.Filter(
                events,
                new UnityLogsReadFilter(
                    AfterSequence: null,
                    Tail: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: "SocketException",
                    QueryTarget: IpcLogQueryTarget.Stack,
                    Source: null,
                    StackTraceMode: IpcUnityLogStackTraceMode.All,
                    StackTraceMaxFrames: 2,
                    StackTraceMaxChars: null));

            Assert.That(filtered, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void Filter_WhenAfterAndSinceSpecified_AfterHasPriority ()
        {
            var queryEngine = new UnityLogsReadQueryEngine();

            var filtered = queryEngine.Filter(
                CreateEvents(),
                new UnityLogsReadFilter(
                    AfterSequence: 2,
                    Tail: null,
                    Since: DateTimeOffset.Parse("2030-01-01T00:00:00+00:00"),
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: IpcLogQueryTarget.Message,
                    Source: null,
                    StackTraceMode: IpcUnityLogStackTraceMode.All,
                    StackTraceMaxFrames: null,
                    StackTraceMaxChars: null));

            Assert.That(filtered.Count, Is.EqualTo(1));
            Assert.That(filtered[0].Message, Is.EqualTo("compile warning"));
        }

        private static IReadOnlyList<UnityLogEvent> CreateEvents ()
        {
            return new List<UnityLogEvent>
            {
                new UnityLogEvent(
                    Sequence: 1,
                    Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 22, TimeSpan.FromHours(9)),
                    Level: IpcLogLevel.Error,
                    Source: IpcUnityLogSource.Runtime,
                    Message: "runtime error",
                    StackTrace: "SocketException: broken pipe",
                    Cursor: "stream-1:1"),
                new UnityLogEvent(
                    Sequence: 2,
                    Timestamp: new DateTimeOffset(2026, 3, 5, 10, 36, 22, TimeSpan.FromHours(9)),
                    Level: IpcLogLevel.Warning,
                    Source: IpcUnityLogSource.Compile,
                    Message: "compile warning",
                    StackTrace: null,
                    Cursor: "stream-1:2"),
            };
        }
    }
}
