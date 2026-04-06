using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorLifecycleStateResolverTests
    {
        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenStartupPending_ReturnsStartingOnceThenReady ()
        {
            var isStartupPending = true;

            var first = UnityEditorLifecycleStateResolver.Resolve(
                ref isStartupPending,
                isShuttingDown: false,
                isDomainReloading: false,
                isCompiling: false,
                isUpdating: false);
            var second = UnityEditorLifecycleStateResolver.Resolve(
                ref isStartupPending,
                isShuttingDown: false,
                isDomainReloading: false,
                isCompiling: false,
                isUpdating: false);

            Assert.That(first, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(second, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(isStartupPending, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenCompiling_DoesNotConsumeStartupPending ()
        {
            var isStartupPending = true;

            var compiling = UnityEditorLifecycleStateResolver.Resolve(
                ref isStartupPending,
                isShuttingDown: false,
                isDomainReloading: false,
                isCompiling: true,
                isUpdating: false);
            var starting = UnityEditorLifecycleStateResolver.Resolve(
                ref isStartupPending,
                isShuttingDown: false,
                isDomainReloading: false,
                isCompiling: false,
                isUpdating: false);

            Assert.That(compiling, Is.EqualTo(IpcEditorLifecycleStateCodec.Compiling));
            Assert.That(starting, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenShuttingDown_TakesPriorityOverOtherStates ()
        {
            var isStartupPending = true;

            var actual = UnityEditorLifecycleStateResolver.Resolve(
                ref isStartupPending,
                isShuttingDown: true,
                isDomainReloading: true,
                isCompiling: true,
                isUpdating: true);

            Assert.That(actual, Is.EqualTo(IpcEditorLifecycleStateCodec.ShuttingDown));
            Assert.That(isStartupPending, Is.True);
        }
    }
}
