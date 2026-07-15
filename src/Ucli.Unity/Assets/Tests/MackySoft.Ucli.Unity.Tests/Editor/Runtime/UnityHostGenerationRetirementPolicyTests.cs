using System;
using System.Collections;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityHostGenerationRetirementPolicyTests
    {
        [Test]
        [Category("Size.Small")]
        public void WaitWithinForegroundDeadlineAsync_WhenRetirementIsComplete_ReturnsTrue ()
        {
            var retired = UnityHostGenerationRetirementPolicy
                .WaitWithinForegroundDeadlineAsync(Task.CompletedTask)
                .GetAwaiter()
                .GetResult();

            Assert.That(retired, Is.True);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator WaitWithinForegroundDeadlineAsync_WhenRetirementDoesNotComplete_ReturnsFalse () => UniTask.ToCoroutine(async () =>
        {
            var retirementSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var retired = await UnityHostGenerationRetirementPolicy
                .WaitWithinForegroundDeadlineAsync(retirementSource.Task);

            Assert.That(retired, Is.False);
            Assert.That(retirementSource.Task.IsCompleted, Is.False);
            retirementSource.SetResult(true);
        });

        [Test]
        [Category("Size.Small")]
        public void WaitWithinForegroundDeadlineAsync_WhenRetirementFaults_PropagatesFailure ()
        {
            var retirementTask = Task.FromException(new InvalidOperationException("retirement failed"));

            var exception = Assert.Throws<InvalidOperationException>(() => UnityHostGenerationRetirementPolicy
                .WaitWithinForegroundDeadlineAsync(retirementTask)
                .GetAwaiter()
                .GetResult());

            Assert.That(exception!.Message, Is.EqualTo("retirement failed"));
        }
    }
}
