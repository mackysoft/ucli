using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorReadinessGateTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        private static readonly (string LifecycleState, string BlockingReason, UcliCode ExpectedErrorCode, string ExpectedMessage)[] BlockedLifecycleStateCases =
        {
            (
                IpcEditorLifecycleStateCodec.Starting,
                IpcEditorBlockingReasonCodec.Startup,
                EditorLifecycleErrorCodes.EditorStarting,
                "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.Recovering,
                IpcEditorBlockingReasonCodec.Recovery,
                EditorLifecycleErrorCodes.EditorRecovering,
                "Unity editor daemon endpoint is recovering. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.Busy,
                IpcEditorBlockingReasonCodec.Busy,
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.Compiling,
                IpcEditorBlockingReasonCodec.Compile,
                EditorLifecycleErrorCodes.EditorCompiling,
                "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.CompileFailed,
                IpcEditorBlockingReasonCodec.CompileFailed,
                EditorLifecycleErrorCodes.EditorCompileFailed,
                "Unity editor has script compilation errors. Fix compiler errors and wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.DomainReloading,
                IpcEditorBlockingReasonCodec.DomainReload,
                EditorLifecycleErrorCodes.EditorDomainReloading,
                "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.Reimporting,
                IpcEditorBlockingReasonCodec.Reimport,
                EditorLifecycleErrorCodes.EditorReimporting,
                "Unity editor is refreshing or reimporting assets. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.Playmode,
                IpcEditorBlockingReasonCodec.PlayMode,
                EditorLifecycleErrorCodes.EditorPlaymode,
                "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.ModalBlocked,
                IpcEditorBlockingReasonCodec.ModalDialog,
                EditorLifecycleErrorCodes.EditorModalBlocked,
                "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.SafeMode,
                IpcEditorBlockingReasonCodec.SafeMode,
                EditorLifecycleErrorCodes.EditorSafeMode,
                "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleStateCodec.ShuttingDown,
                IpcEditorBlockingReasonCodec.Shutdown,
                EditorLifecycleErrorCodes.EditorShuttingDown,
                "Unity editor is shutting down and cannot accept execution requests."),
            (
                IpcEditorLifecycleStateCodec.Unavailable,
                IpcEditorBlockingReasonCodec.Unavailable,
                EditorLifecycleErrorCodes.EditorUnavailable,
                "Unity editor lifecycle is unavailable because the daemon endpoint cannot be observed."),
        };

        [TestCase(IpcEditorLifecycleStateCodec.Starting, true)]
        [TestCase(IpcEditorLifecycleStateCodec.Recovering, true)]
        [TestCase(IpcEditorLifecycleStateCodec.Busy, true)]
        [TestCase(IpcEditorLifecycleStateCodec.Compiling, true)]
        [TestCase(IpcEditorLifecycleStateCodec.DomainReloading, true)]
        [TestCase(IpcEditorLifecycleStateCodec.Reimporting, true)]
        [TestCase(IpcEditorLifecycleStateCodec.CompileFailed, false)]
        [TestCase(IpcEditorLifecycleStateCodec.ModalBlocked, false)]
        [TestCase(IpcEditorLifecycleStateCodec.SafeMode, false)]
        [TestCase(IpcEditorLifecycleStateCodec.Playmode, false)]
        [TestCase(IpcEditorLifecycleStateCodec.Ready, false)]
        [TestCase(IpcEditorLifecycleStateCodec.ShuttingDown, false)]
        [TestCase(IpcEditorLifecycleStateCodec.Unavailable, false)]
        [Category("Size.Small")]
        public void IsWaitableState_WhenLifecycleStateMatchesPolicy_ReturnsExpectedValue (
            string lifecycleState,
            bool expected)
        {
            var actual = UnityEditorExecutionReadinessPolicy.IsWaitableState(lifecycleState);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        [Category("Size.Small")]
        public void CreateBlockedResult_WhenLifecycleStateIsMapped_ReturnsExpectedError ()
        {
            foreach (var testCase in BlockedLifecycleStateCases)
            {
                var snapshot = new UnityEditorLifecycleSnapshot(
                    EditorMode: DaemonEditorMode.Batchmode,
                    LifecycleState: testCase.LifecycleState,
                    BlockingReason: testCase.BlockingReason,
                    CompileState: IpcCompileStateCodec.Ready,
                    CompileGeneration: "2",
                    DomainReloadGeneration: "3",
                    CanAcceptExecutionRequests: false);

                var result = UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot);

                Assert.That(result.IsReady, Is.False, testCase.LifecycleState);
                Assert.That(result.Snapshot, Is.EqualTo(snapshot), testCase.LifecycleState);
                Assert.That(result.Error, Is.Not.Null, testCase.LifecycleState);
                Assert.That(result.Error!.Code, Is.EqualTo(testCase.ExpectedErrorCode), testCase.LifecycleState);
                Assert.That(result.Error.Message, Is.EqualTo(testCase.ExpectedMessage), testCase.LifecycleState);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenStartupIsPending_DoesNotConsumeStarting ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                isPlaymodeActive: false,
                out _);

            var first = gate.CaptureSnapshot();
            var second = gate.CaptureSnapshot();

            Assert.That(first.EditorMode, Is.EqualTo(DaemonEditorMode.Batchmode));
            Assert.That(first.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(first.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.Startup));
            Assert.That(first.CanAcceptExecutionRequests, Is.False);
            Assert.That(first.CompileGeneration, Is.EqualTo("4"));
            Assert.That(first.DomainReloadGeneration, Is.EqualTo("9"));
            Assert.That(second.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(second.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.Startup));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenEditorUpdateCompletesStartup_ReturnsReadySnapshot ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                isPlaymodeActive: false,
                out var lifecycleTelemetryState);

            var beforeUpdate = gate.CaptureSnapshot();
            lifecycleTelemetryState.ObserveEditorUpdate(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            var afterUpdate = gate.CaptureSnapshot();

            Assert.That(beforeUpdate.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Starting));
            Assert.That(afterUpdate.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(afterUpdate.BlockingReason, Is.Null);
            Assert.That(afterUpdate.CanAcceptExecutionRequests, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenEditorUpdateCompletesRecovery_ReturnsReadySnapshot ()
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isRecoveringPending: true);
            var gate = CreateGate(
                DaemonEditorMode.Gui,
                telemetryState,
                static () => false,
                static () => false,
                static () => false);

            var beforeUpdate = gate.CaptureSnapshot();
            telemetryState.ObserveEditorUpdate(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            var afterUpdate = gate.CaptureSnapshot();

            Assert.That(beforeUpdate.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Recovering));
            Assert.That(beforeUpdate.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.Recovery));
            Assert.That(afterUpdate.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(afterUpdate.BlockingReason, Is.Null);
            Assert.That(afterUpdate.CanAcceptExecutionRequests, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenEditorModeIsGui_ReturnsGuiEditorMode ()
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);
            var gate = CreateGate(
                DaemonEditorMode.Gui,
                telemetryState,
                static () => false,
                static () => false,
                static () => false);

            var snapshot = gate.CaptureSnapshot();

            Assert.That(snapshot.EditorMode, Is.EqualTo(DaemonEditorMode.Gui));
            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.True);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CaptureSnapshot_WhenPlaymodeIsActive_ReturnsPlaymodeSnapshot () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 5,
                domainReloadGeneration: 12,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: true,
                out _);

            var snapshot = gate.CaptureSnapshot();

            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(snapshot.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.PlayMode));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(snapshot.PlayMode, Is.Not.Null);
            Assert.That(snapshot.PlayMode.State, Is.EqualTo(IpcPlayModeStateNames.Playing));
            Assert.That(snapshot.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(snapshot.PlayMode.IsPlaying, Is.True);
            Assert.That(snapshot.PlayMode.IsPlayingOrWillChangePlaymode, Is.True);
        });

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenBatchmodeIsStopped_ReturnsStoppedPlayModeSnapshot ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                out _);

            var snapshot = gate.CaptureSnapshot();

            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(snapshot.PlayMode, Is.Not.Null);
            Assert.That(snapshot.PlayMode.State, Is.EqualTo(IpcPlayModeStateNames.Stopped));
            Assert.That(snapshot.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(snapshot.PlayMode.IsPlaying, Is.False);
            Assert.That(snapshot.PlayMode.IsPlayingOrWillChangePlaymode, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenPlayModeChangeIsPending_ReturnsPlaymodeEnteringSnapshot ()
        {
            var monitor = CreateMonitor(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: true);

            var snapshot = monitor.CaptureSnapshot(DaemonEditorMode.Batchmode);

            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(snapshot.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.PlayMode));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(snapshot.PlayMode.State, Is.EqualTo(IpcPlayModeStateNames.Entering));
            Assert.That(snapshot.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(snapshot.PlayMode.IsPlaying, Is.False);
            Assert.That(snapshot.PlayMode.IsPlayingOrWillChangePlaymode, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureSnapshot_WhenPlayModeFlagsAreInconsistent_ReturnsPlaymodeWithUnknownPlayModeState ()
        {
            var monitor = CreateMonitor(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: false);

            var snapshot = monitor.CaptureSnapshot(DaemonEditorMode.Batchmode);

            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(snapshot.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.PlayMode));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(snapshot.PlayMode.State, Is.EqualTo(IpcPlayModeStateNames.Unknown));
            Assert.That(snapshot.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(snapshot.PlayMode.IsPlaying, Is.True);
            Assert.That(snapshot.PlayMode.IsPlayingOrWillChangePlaymode, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void CapturePlayModeSnapshot_WhenTransitionCallbacksAreObserved_ReturnsTransitionStatesWithoutAdvancingGeneration ()
        {
            UnityEditorSessionStateStore.SetPlayModeGenerationForTests(40);
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 1,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                playModeGeneration: 40);

            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.ExitingEditMode);
            var entering = telemetryState.CapturePlayModeSnapshot(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: true);
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            var exiting = telemetryState.CapturePlayModeSnapshot(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true);

            Assert.That(entering.State, Is.EqualTo(IpcPlayModeStateNames.Entering));
            Assert.That(entering.Transition, Is.EqualTo(IpcPlayModeTransitionNames.Entering));
            Assert.That(entering.Generation, Is.EqualTo("40"));
            Assert.That(exiting.State, Is.EqualTo(IpcPlayModeStateNames.Exiting));
            Assert.That(exiting.Transition, Is.EqualTo(IpcPlayModeTransitionNames.Exiting));
            Assert.That(exiting.Generation, Is.EqualTo("40"));
        }

        [Test]
        [Category("Size.Small")]
        public void CapturePlayModeSnapshot_WhenEnterAndExitComplete_AdvancesGeneration ()
        {
            UnityEditorSessionStateStore.SetPlayModeGenerationForTests(100);
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 1,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                playModeGeneration: 100);

            var before = telemetryState.CapturePlayModeSnapshot(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false);
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.ExitingEditMode);
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
            var entered = telemetryState.CapturePlayModeSnapshot(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true);
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            var exited = telemetryState.CapturePlayModeSnapshot(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false);

            Assert.That(before.Generation, Is.EqualTo("100"));
            Assert.That(entered.State, Is.EqualTo(IpcPlayModeStateNames.Playing));
            Assert.That(entered.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(entered.Generation, Is.EqualTo("101"));
            Assert.That(exited.State, Is.EqualTo(IpcPlayModeStateNames.Stopped));
            Assert.That(exited.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(exited.Generation, Is.EqualTo("102"));
        }

        [Test]
        [Category("Size.Small")]
        public void CapturePlayModeSnapshot_WhenStableStateChangesAfterReload_AdvancesGeneration ()
        {
            UnityEditorSessionStateStore.SetPlayModeGenerationForTests(200);
            UnityEditorSessionStateStore.SetPlayModeStableStateForTests(IpcPlayModeState.Stopped);
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 1,
                domainReloadGeneration: 2,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);

            var entered = telemetryState.CapturePlayModeSnapshot(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true);
            var repeated = telemetryState.CapturePlayModeSnapshot(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true);

            Assert.That(entered.State, Is.EqualTo(IpcPlayModeStateNames.Playing));
            Assert.That(entered.Transition, Is.EqualTo(IpcPlayModeTransitionNames.None));
            Assert.That(entered.Generation, Is.EqualTo("201"));
            Assert.That(repeated.Generation, Is.EqualTo("201"));
            Assert.That(UnityEditorSessionStateStore.RestorePlayModeStableState(), Is.EqualTo(IpcPlayModeState.Playing));
        }

        [Test]
        [Category("Size.Small")]
        public void CapturePlayModeSnapshot_WhenNoPriorStableState_SeedsStateWithoutAdvancingGeneration ()
        {
            UnityEditorSessionStateStore.SetPlayModeGenerationForTests(300);
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 1,
                domainReloadGeneration: 2,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);

            var entered = telemetryState.CapturePlayModeSnapshot(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true);

            Assert.That(entered.State, Is.EqualTo(IpcPlayModeStateNames.Playing));
            Assert.That(entered.Generation, Is.EqualTo("300"));
            Assert.That(UnityEditorSessionStateStore.RestorePlayModeStableState(), Is.EqualTo(IpcPlayModeState.Playing));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator CaptureSnapshot_WhenEditorIsIdle_ReturnsReadySnapshot () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 5,
                domainReloadGeneration: 12,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                out _);

            var snapshot = gate.CaptureSnapshot();

            Assert.That(snapshot.EditorMode, Is.EqualTo(DaemonEditorMode.Batchmode));
            Assert.That(snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(snapshot.BlockingReason, Is.Null);
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.True);
            Assert.That(snapshot.CompileGeneration, Is.EqualTo("5"));
            Assert.That(snapshot.DomainReloadGeneration, Is.EqualTo("12"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenFailFastIsDisabled_WaitsUntilEditorBecomesReady () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 6,
                domainReloadGeneration: 13,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: true,
                out _,
                out var activityProbe);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            activityProbe.IsUpdating = false;
            await UniTask.Yield();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate default-wait completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenStartupIsPending_DoesNotConsumeStartingBeforeNextEditorUpdate () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 7,
                domainReloadGeneration: 14,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false,
                out _,
                out _);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            await UniTask.Yield();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate startup update completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenPlaymodeIsActive_ReturnsBlockedResultWithoutWaiting () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 8,
                domainReloadGeneration: 15,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: true,
                out _);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.True);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate playmode blocked result",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorPlaymode));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenAllowPlayModeUsesGuiPlaymode_ReturnsReadyResult () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                DaemonEditorMode.Gui,
                isPlaymodeActive: true);

            var result = await TestAwaiter.WaitAsync(
                gate.EnsureExecutionReadyAsync(failFast: false, allowPlayMode: true),
                "Readiness gate allow Play Mode ready result",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Snapshot.EditorMode, Is.EqualTo(DaemonEditorMode.Gui));
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenAllowPlayModeIsOnlyTransitioning_ReturnsPlayModeRequiredError () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                DaemonEditorMode.Gui,
                isPlaymodeLifecycleActive: true,
                isPlayModeMutationActive: false);

            var result = await TestAwaiter.WaitAsync(
                gate.EnsureExecutionReadyAsync(failFast: false, allowPlayMode: true),
                "Readiness gate allow Play Mode transition result",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Playmode));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeNotActive));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenAllowPlayModeIsCompiling_ReturnsCompilingError () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                DaemonEditorMode.Gui,
                isPlaymodeLifecycleActive: true,
                isPlayModeMutationActive: true,
                isCompiling: true);

            var result = await TestAwaiter.WaitAsync(
                gate.EnsureExecutionReadyAsync(failFast: false, allowPlayMode: true),
                "Readiness gate allow Play Mode compiling result",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Compiling));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorCompiling));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenAllowPlayModeUsesBatchmodePlaymode_ReturnsGuiRequirementError () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                DaemonEditorMode.Batchmode,
                isPlaymodeActive: true);

            var result = await TestAwaiter.WaitAsync(
                gate.EnsureExecutionReadyAsync(failFast: false, allowPlayMode: true),
                "Readiness gate allow Play Mode batchmode result",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeRequiresGuiEditor));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenAllowPlayModeUsesGuiReady_ReturnsPlayModeRequiredError () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                DaemonEditorMode.Gui,
                isPlaymodeActive: false);

            var result = await TestAwaiter.WaitAsync(
                gate.EnsureExecutionReadyAsync(failFast: false, allowPlayMode: true),
                "Readiness gate allow Play Mode inactive result",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(PlayModeErrorCodes.PlayModeNotActive));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenDomainReloading_WaitsUntilReloadCompletes () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 8,
                domainReloadGeneration: 15,
                isDomainReloading: true,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                out var lifecycleTelemetryState);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            lifecycleTelemetryState.SetDomainReloading(false);
            await UniTask.Yield();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate domain reload completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Ready));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenCanceled_PropagatesCancellation () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 9,
                domainReloadGeneration: 16,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: true,
                out _,
                out _);
            using var cancellationTokenSource = new CancellationTokenSource();

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false, cancellationTokenSource.Token);
            Assert.That(resultTask.IsCompleted, Is.False);

            cancellationTokenSource.Cancel();
            _ = await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    resultTask,
                    "Readiness gate cancellation",
                    AsyncWaitTimeout);
            }, "Readiness gate cancellation result", AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenAssemblyReloadStartsDuringWait_ReturnsDomainReloadBlockedResult () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 10,
                domainReloadGeneration: 17,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: true,
                out _,
                out _,
                out var waitSignalBus);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            waitSignalBus.RaiseBeforeAssemblyReload();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate assembly reload boundary",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.DomainReloading));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorDomainReloading));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenEditorQuitsDuringWait_ReturnsShuttingDownBlockedResult () => UniTask.ToCoroutine(async () =>
        {
            var gate = CreateGate(
                compileGeneration: 11,
                domainReloadGeneration: 18,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: true,
                out _,
                out _,
                out var waitSignalBus);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            waitSignalBus.RaiseQuitting();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate shutdown boundary",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Snapshot.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.ShuttingDown));
            Assert.That(result.Snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorShuttingDown));
        });

        private static UnityEditorReadinessGate CreateGate (
            DaemonEditorMode editorMode,
            bool isPlaymodeActive)
        {
            return CreateGate(
                editorMode,
                isPlaymodeLifecycleActive: isPlaymodeActive,
                isPlayModeMutationActive: isPlaymodeActive);
        }

        private static UnityEditorReadinessGate CreateGate (
            DaemonEditorMode editorMode,
            bool isPlaymodeLifecycleActive,
            bool isPlayModeMutationActive,
            bool isCompiling = false)
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 1,
                domainReloadGeneration: 1,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);
            return new UnityEditorReadinessGate(
                editorMode,
                new UnityEditorLifecycleMonitor(
                    telemetryState,
                    () => isCompiling,
                    static () => false,
                    () => isPlaymodeLifecycleActive,
                    () => isPlaymodeLifecycleActive),
                () => isPlayModeMutationActive,
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);
        }

        private static UnityEditorReadinessGate CreateGate (
            int compileGeneration,
            int domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            bool isPlaymodeActive,
            out UnityEditorLifecycleTelemetryState lifecycleTelemetryState)
        {
            return CreateGate(
                compileGeneration,
                domainReloadGeneration,
                isDomainReloading,
                isShuttingDown,
                isStartupPending,
                isPlaymodeActive,
                isCompiling: false,
                isUpdating: false,
                out lifecycleTelemetryState,
                out _);
        }

        private static UnityEditorReadinessGate CreateGate (
            int compileGeneration,
            int domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            bool isPlaymodeActive,
            bool isCompiling,
            bool isUpdating,
            out UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            out EditorActivityProbe activityProbe)
        {
            return CreateGate(
                compileGeneration,
                domainReloadGeneration,
                isDomainReloading,
                isShuttingDown,
                isStartupPending,
                isPlaymodeActive,
                isCompiling,
                isUpdating,
                out lifecycleTelemetryState,
                out activityProbe,
                out _);
        }

        private static UnityEditorReadinessGate CreateGate (
            int compileGeneration,
            int domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            bool isPlaymodeActive,
            bool isCompiling,
            bool isUpdating,
            out UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            out EditorActivityProbe activityProbe,
            out WaitSignalBus waitSignalBus)
        {
            var probe = new EditorActivityProbe
            {
                IsCompiling = isCompiling,
                IsUpdating = isUpdating,
                IsPlaymodeActive = isPlaymodeActive,
            };
            var signalBus = new WaitSignalBus();
            activityProbe = probe;
            waitSignalBus = signalBus;
            lifecycleTelemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration,
                domainReloadGeneration,
                isDomainReloading,
                isShuttingDown,
                isStartupPending);
            return new UnityEditorReadinessGate(
                DaemonEditorMode.Batchmode,
                new UnityEditorLifecycleMonitor(
                    lifecycleTelemetryState,
                    () => probe.IsCompiling,
                    () => probe.IsUpdating,
                    () => probe.IsPlaymodeActive,
                    () => probe.IsPlaymodeActive),
                () => probe.IsPlaymodeActive,
                signalBus.SubscribeBeforeAssemblyReload,
                signalBus.UnsubscribeBeforeAssemblyReload,
                signalBus.SubscribeQuitting,
                signalBus.UnsubscribeQuitting,
                subscribeToEditorEvents: false);
        }

        private static UnityEditorReadinessGate CreateGate (
            DaemonEditorMode editorMode,
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider,
            Func<bool> isPlaymodeActiveProvider)
        {
            return new UnityEditorReadinessGate(
                editorMode,
                new UnityEditorLifecycleMonitor(
                    lifecycleTelemetryState,
                    isCompilingProvider,
                    isUpdatingProvider,
                    isPlaymodeActiveProvider,
                    isPlaymodeActiveProvider),
                isPlaymodeActiveProvider,
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);
        }

        private static UnityEditorLifecycleMonitor CreateMonitor (
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            var lifecycleTelemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);

            return new UnityEditorLifecycleMonitor(
                lifecycleTelemetryState,
                static () => false,
                static () => false,
                () => isPlaying,
                () => isPlayingOrWillChangePlaymode);
        }

        private sealed class EditorActivityProbe
        {
            public bool IsCompiling { get; set; }

            public bool IsUpdating { get; set; }

            public bool IsPlaymodeActive { get; set; }
        }

        private sealed class WaitSignalBus
        {
            private event AssemblyReloadEvents.AssemblyReloadCallback BeforeAssemblyReload;

            private event Action Quitting;

            public void SubscribeBeforeAssemblyReload (AssemblyReloadEvents.AssemblyReloadCallback handler)
            {
                BeforeAssemblyReload += handler;
            }

            public void UnsubscribeBeforeAssemblyReload (AssemblyReloadEvents.AssemblyReloadCallback handler)
            {
                BeforeAssemblyReload -= handler;
            }

            public void SubscribeQuitting (Action handler)
            {
                Quitting += handler;
            }

            public void UnsubscribeQuitting (Action handler)
            {
                Quitting -= handler;
            }

            public void RaiseBeforeAssemblyReload ()
            {
                BeforeAssemblyReload?.Invoke();
            }

            public void RaiseQuitting ()
            {
                Quitting?.Invoke();
            }
        }
    }
}
