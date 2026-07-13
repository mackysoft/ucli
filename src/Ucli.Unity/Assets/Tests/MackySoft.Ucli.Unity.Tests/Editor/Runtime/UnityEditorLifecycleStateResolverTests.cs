using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
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
                isPlaymodeActive: false,
                isDomainReloading: false,
                isCompiling: false,
                hasCompileFailure: false,
                isUpdating: false,
                isRecoveringPending: false);
            var second = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
                isShuttingDown: false,
                isPlaymodeActive: false,
                isDomainReloading: false,
                isCompiling: false,
                hasCompileFailure: false,
                isUpdating: false,
                isRecoveringPending: false);

            Assert.That(first, Is.EqualTo(IpcEditorLifecycleState.Starting));
            Assert.That(second, Is.EqualTo(IpcEditorLifecycleState.Starting));
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
                isPlaymodeActive: false,
                isDomainReloading: false,
                isCompiling: true,
                hasCompileFailure: false,
                isUpdating: false,
                isRecoveringPending: false);
            var starting = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
                isShuttingDown: false,
                isPlaymodeActive: false,
                isDomainReloading: false,
                isCompiling: false,
                hasCompileFailure: false,
                isUpdating: false,
                isRecoveringPending: false);

            Assert.That(compiling, Is.EqualTo(IpcEditorLifecycleState.Compiling));
            Assert.That(starting, Is.EqualTo(IpcEditorLifecycleState.Starting));
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenShuttingDown_TakesPriorityOverOtherStates ()
        {
            var isStartupPending = true;

            var actual = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
                isShuttingDown: true,
                isPlaymodeActive: true,
                isDomainReloading: true,
                isCompiling: true,
                hasCompileFailure: true,
                isUpdating: true,
                isRecoveringPending: true);

            Assert.That(actual, Is.EqualTo(IpcEditorLifecycleState.ShuttingDown));
            Assert.That(isStartupPending, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenPlaymodeIsChangingDuringReload_ReturnsReloadingBeforePlaymode ()
        {
            var actual = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending: true,
                isShuttingDown: false,
                isPlaymodeActive: true,
                isDomainReloading: true,
                isCompiling: true,
                hasCompileFailure: true,
                isUpdating: true,
                isRecoveringPending: true);

            Assert.That(actual, Is.EqualTo(IpcEditorLifecycleState.DomainReloading));
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenCompileFailed_ReturnsCompileFailedBeforeReimporting ()
        {
            var actual = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending: false,
                isShuttingDown: false,
                isPlaymodeActive: false,
                isDomainReloading: false,
                isCompiling: false,
                hasCompileFailure: true,
                isUpdating: true,
                isRecoveringPending: true);

            Assert.That(actual, Is.EqualTo(IpcEditorLifecycleState.CompileFailed));
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenUpdating_ReturnsReimporting ()
        {
            var actual = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending: false,
                isShuttingDown: false,
                isPlaymodeActive: false,
                isDomainReloading: false,
                isCompiling: false,
                hasCompileFailure: false,
                isUpdating: true,
                isRecoveringPending: false);

            Assert.That(actual, Is.EqualTo(IpcEditorLifecycleState.Reimporting));
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WhenRecoveringPending_ReturnsRecovering ()
        {
            var actual = UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending: false,
                isShuttingDown: false,
                isPlaymodeActive: false,
                isDomainReloading: false,
                isCompiling: false,
                hasCompileFailure: false,
                isUpdating: false,
                isRecoveringPending: true);

            Assert.That(actual, Is.EqualTo(IpcEditorLifecycleState.Recovering));
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
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            telemetryState.ObserveEditorUpdate(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            var afterUpdate = telemetryState.ResolveLifecycleState(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);

            Assert.That(beforeUpdate, Is.EqualTo(IpcEditorLifecycleState.Starting));
            Assert.That(afterUpdate, Is.EqualTo(IpcEditorLifecycleState.Ready));
        }

        [Test]
        [Category("Size.Small")]
        public void ObserveEditorUpdate_WhenStartupPendingAndPlaymodeIsActive_ClearsStartupPending ()
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 0,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true);

            var beforeUpdate = telemetryState.ResolveLifecycleState(
                isPlaymodeActive: true,
                isCompiling: false,
                isUpdating: false);
            telemetryState.ObserveEditorUpdate(
                isPlaymodeActive: true,
                isCompiling: false,
                isUpdating: false);
            var afterUpdate = telemetryState.ResolveLifecycleState(
                isPlaymodeActive: true,
                isCompiling: false,
                isUpdating: false);

            Assert.That(beforeUpdate, Is.EqualTo(IpcEditorLifecycleState.Starting));
            Assert.That(afterUpdate, Is.EqualTo(IpcEditorLifecycleState.PlayMode));
        }
    }
}
