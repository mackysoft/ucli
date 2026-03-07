using System;

using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityLogCollectorTests
    {
        [Test]
        [Category("Size.Small")]
        public void HandleRuntimeLog_WhenMessageHasDaemonPrefix_IgnoresEvent ()
        {
            var stream = new UnityLogRingBuffer();
            var collector = new UnityLogCollector(stream, new UnityCompileMessageDedupeCache());

            collector.HandleRuntimeLog("[ucli][ipc] booted", string.Empty, LogType.Log);

            var snapshot = stream.Snapshot();
            Assert.That(snapshot.Events, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void HandleCompileMessage_ThenMatchingRuntimeLog_DeduplicatesRuntimeCopy ()
        {
            var stream = new UnityLogRingBuffer();
            var collector = new UnityLogCollector(stream, new UnityCompileMessageDedupeCache());
            var compileMessage = new CompilerMessage
            {
                file = "Assets/Test.cs",
                line = 12,
                column = 5,
                message = "CS1001: ; expected",
                type = CompilerMessageType.Error,
            };

            collector.HandleCompileMessage(compileMessage);
            collector.HandleRuntimeLog("Assets/Test.cs(12,5): error CS1001: ; expected", "stack", LogType.Error);

            var snapshot = stream.Snapshot();
            Assert.That(snapshot.Events.Count, Is.EqualTo(1));
            Assert.That(snapshot.Events[0].Source, Is.EqualTo(IpcUnityLogsSourceCodec.Compile));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("Assets/Test.cs(12,5): error CS1001: ; expected"));
        }

        [Test]
        [Category("Size.Small")]
        public void DaemonLoggerException_WhenEmitted_DoesNotProduceUnprefixedRuntimeExceptionCopy ()
        {
            var daemonStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(daemonStream);
            var exception = new InvalidOperationException("boom");

            LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\A\[ucli\]\[ipc\] daemon failed\r?\nSystem\.InvalidOperationException: boom", System.Text.RegularExpressions.RegexOptions.Singleline));

            daemonLogger.Exception("ipc", "daemon failed", exception);

            var snapshot = daemonStream.Snapshot();
            Assert.That(snapshot.Events.Count, Is.EqualTo(1));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("daemon failed"));
            Assert.That(snapshot.Events[0].Raw, Does.Contain("System.InvalidOperationException: boom"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
