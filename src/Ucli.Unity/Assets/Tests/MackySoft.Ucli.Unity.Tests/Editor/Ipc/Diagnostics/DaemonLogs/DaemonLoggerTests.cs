using System;
using System.Collections.Generic;

using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DaemonLoggerTests
    {
        [Test]
        [Category("Size.Small")]
        public void Exception_WhenEmitted_DoesNotProduceUnprefixedRuntimeExceptionCopy ()
        {
            var daemonStream = new DaemonLogRingBuffer();
            var errorLogs = new List<string>();
            var daemonLogger = new DaemonLogger(
                daemonStream,
                logInfo: _ => { },
                logWarning: _ => { },
                logError: errorLogs.Add);
            var exception = new InvalidOperationException("boom");

            daemonLogger.Exception("ipc", "daemon failed", exception);

            var snapshot = daemonStream.Snapshot();
            Assert.That(snapshot.Events.Count, Is.EqualTo(1));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("daemon failed"));
            Assert.That(snapshot.Events[0].Raw, Does.Contain("System.InvalidOperationException: boom"));
            Assert.That(errorLogs.Count, Is.EqualTo(1));
            Assert.That(errorLogs[0], Does.StartWith("[ucli][ipc] daemon failed"));
            Assert.That(errorLogs[0], Does.Not.StartWith("System.InvalidOperationException: boom"));
            Assert.That(errorLogs[0], Does.Contain("System.InvalidOperationException: boom"));
        }
    }
}
