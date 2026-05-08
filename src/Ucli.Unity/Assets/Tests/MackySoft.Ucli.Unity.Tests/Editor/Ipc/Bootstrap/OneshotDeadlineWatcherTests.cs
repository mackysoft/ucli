using System;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class OneshotDeadlineWatcherTests
    {
        [Test]
        [Category("Size.Small")]
        public void OnEditorUpdate_WhenDeadlinePassed_CompletesWaitAsyncAndRequestsExit ()
        {
            var now = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
            var editorTime = 0d;
            using var watcher = new OneshotDeadlineWatcher(
                now.AddSeconds(-1),
                () => now,
                () => editorTime);

            watcher.OnEditorUpdate();

            Assert.That(watcher.HasRequestedExit, Is.True);
            Assert.That(watcher.WaitAsync().IsCompleted, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void OnEditorUpdate_WhenBeforeDeadline_DoesNotCompleteWaitAsync ()
        {
            var now = new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero);
            var editorTime = 0d;
            using var watcher = new OneshotDeadlineWatcher(
                now.AddSeconds(1),
                () => now,
                () => editorTime);

            watcher.OnEditorUpdate();

            Assert.That(watcher.HasRequestedExit, Is.False);
            Assert.That(watcher.WaitAsync().IsCompleted, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Dispose_WhenDeadlinePassesAfterDisposal_DoesNotCompleteWaitAsync ()
        {
            var now = new DateTimeOffset(2026, 03, 09, 0, 0, 1, TimeSpan.Zero);
            var editorTime = 0d;
            var watcher = new OneshotDeadlineWatcher(
                now.AddSeconds(-1),
                () => now,
                () => editorTime);

            watcher.Dispose();
            watcher.OnEditorUpdate();

            Assert.That(watcher.HasRequestedExit, Is.False);
            Assert.That(watcher.WaitAsync().IsCompleted, Is.False);
        }
    }
}
