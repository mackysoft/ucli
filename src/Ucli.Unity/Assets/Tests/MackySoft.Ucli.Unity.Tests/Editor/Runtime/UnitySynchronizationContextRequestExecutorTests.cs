using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnitySynchronizationContextRequestExecutorTests
    {
        [UnityTest]
        public IEnumerator ExecuteAsync_WhenCapturedSynchronizationContextDropsPostedWork_ProcessesQueueFromEditorUpdate ()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            using var executor = new UnitySynchronizationContextRequestExecutor(
                new DroppingSynchronizationContext(),
                mainThreadId);

            var executionTask = Task.Run(() => executor.ExecuteAsync(
                () => Task.FromResult(Thread.CurrentThread.ManagedThreadId),
                CancellationToken.None));

            var deadlineUtc = DateTime.UtcNow.AddSeconds(5);
            while (!executionTask.IsCompleted && DateTime.UtcNow < deadlineUtc)
            {
                yield return null;
            }

            Assert.IsTrue(executionTask.IsCompleted, "Queued Unity main-thread work was not processed from the editor update pump.");
            Assert.IsFalse(executionTask.IsCanceled);
            if (executionTask.IsFaulted)
            {
                throw executionTask.Exception!;
            }

            Assert.AreEqual(mainThreadId, executionTask.Result);
        }

        private sealed class DroppingSynchronizationContext : SynchronizationContext
        {
            public override void Post (
                SendOrPostCallback d,
                object state)
            {
            }
        }
    }
}
