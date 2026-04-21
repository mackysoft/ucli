using System;
using System.Text.RegularExpressions;

using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DaemonLoggerTests
    {
        [Test]
        [Category("Size.Small")]
        public void Exception_WhenEmitted_DoesNotProduceUnprefixedRuntimeExceptionCopy ()
        {
            var daemonStream = new DaemonLogRingBuffer();
            var daemonLogger = new DaemonLogger(daemonStream);
            var exception = new InvalidOperationException("boom");

            LogAssert.Expect(
                LogType.Error,
                new Regex(@"\A\[ucli\]\[ipc\] daemon failed\r?\nSystem\.InvalidOperationException: boom", RegexOptions.Singleline));

            daemonLogger.Exception("ipc", "daemon failed", exception);

            var snapshot = daemonStream.Snapshot();
            Assert.That(snapshot.Events.Count, Is.EqualTo(1));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("daemon failed"));
            Assert.That(snapshot.Events[0].Raw, Does.Contain("System.InvalidOperationException: boom"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}