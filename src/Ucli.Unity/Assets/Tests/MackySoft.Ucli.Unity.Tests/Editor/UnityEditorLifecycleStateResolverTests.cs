using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorLifecycleStateResolverTests
    {
        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenStartupPending_ReturnsStartingWithoutMutatingState ()
        {
            var isStartupPending = true;

            var first = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
                isShuttingDown: false,
                isDomainReloading: false,
                isCompiling: false,
                isUpdating: false);
            var second = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
                isShuttingDown: false,
                isDomainReloading: false,
                isCompiling: false,
                isUpdating: false);

            Assert.That(first, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(second, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(isStartupPending, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenCompiling_DoesNotConsumeStartupPending ()
        {
            var isStartupPending = true;

            var compiling = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
                isShuttingDown: false,
                isDomainReloading: false,
                isCompiling: true,
                isUpdating: false);
            var starting = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
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
                isStartupPending,
                isShuttingDown: true,
                isDomainReloading: true,
                isCompiling: true,
                isUpdating: true);

            Assert.That(actual, Is.EqualTo(IpcEditorLifecycleStateCodec.ShuttingDown));
            Assert.That(isStartupPending, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void ObserveEditorUpdate_WhenStartupPendingAndEditorIsIdle_ClearsStartupPending ()
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 0,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true);

            var beforeUpdate = telemetryState.ResolveLifecycleState(
                isCompiling: false,
                isUpdating: false);
            telemetryState.ObserveEditorUpdate(
                isCompiling: false,
                isUpdating: false);
            var afterUpdate = telemetryState.ResolveLifecycleState(
                isCompiling: false,
                isUpdating: false);

            Assert.That(beforeUpdate, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(afterUpdate, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
        }
    }
}
