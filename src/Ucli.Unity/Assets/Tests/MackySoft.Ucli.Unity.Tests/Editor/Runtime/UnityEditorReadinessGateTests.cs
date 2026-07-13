using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityEditorReadinessGateTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        private static readonly (IpcEditorLifecycleState LifecycleState, IpcEditorBlockingReason BlockingReason, UcliCode ExpectedErrorCode, string ExpectedMessage)[] BlockedLifecycleStateCases =
        {
            (
                IpcEditorLifecycleState.Starting,
                IpcEditorBlockingReason.Startup,
                EditorLifecycleErrorCodes.EditorStarting,
                "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.Recovering,
                IpcEditorBlockingReason.Recovery,
                EditorLifecycleErrorCodes.EditorRecovering,
                "Unity editor daemon endpoint is recovering. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.Busy,
                IpcEditorBlockingReason.Busy,
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.Compiling,
                IpcEditorBlockingReason.Compile,
                EditorLifecycleErrorCodes.EditorCompiling,
                "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.CompileFailed,
                IpcEditorBlockingReason.CompileFailed,
                EditorLifecycleErrorCodes.EditorCompileFailed,
                "Unity editor has script compilation errors. Fix compiler errors and wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.DomainReloading,
                IpcEditorBlockingReason.DomainReload,
                EditorLifecycleErrorCodes.EditorDomainReloading,
                "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.Reimporting,
                IpcEditorBlockingReason.Reimport,
                EditorLifecycleErrorCodes.EditorReimporting,
                "Unity editor is refreshing or reimporting assets. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.PlayMode,
                IpcEditorBlockingReason.PlayMode,
                EditorLifecycleErrorCodes.EditorPlaymode,
                "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.ModalBlocked,
                IpcEditorBlockingReason.ModalDialog,
                EditorLifecycleErrorCodes.EditorModalBlocked,
                "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.SafeMode,
                IpcEditorBlockingReason.SafeMode,
                EditorLifecycleErrorCodes.EditorSafeMode,
                "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request."),
            (
                IpcEditorLifecycleState.ShuttingDown,
                IpcEditorBlockingReason.Shutdown,
                EditorLifecycleErrorCodes.EditorShuttingDown,
                "Unity editor is shutting down and cannot accept execution requests."),
            (
                IpcEditorLifecycleState.Unavailable,
                IpcEditorBlockingReason.Unavailable,
                EditorLifecycleErrorCodes.EditorUnavailable,
                "Unity editor lifecycle is unavailable because the daemon endpoint cannot be observed."),
        };

        [Test]
        [Category("Size.Small")]
        public void UnityEditorObservationConstructor_WhenStateIsNull_ThrowsArgumentNullException ()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new UnityEditorObservation(null, DateTimeOffset.UnixEpoch);
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UnityEditorObservationConstructor_WhenObservedAtUtcIsDefault_ThrowsArgumentOutOfRangeException ()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                _ = new UnityEditorObservation(
                    new UnityEditorStateSnapshot(
                        editorMode: DaemonEditorMode.Batchmode,
                        lifecycleState: IpcEditorLifecycleState.Ready,
                        compileState: IpcCompileState.Ready,
                        generations: new IpcUnityGenerationSnapshot(1, 2, 3, 4),
                        playMode: new IpcPlayModeSnapshot(
                            State: IpcPlayModeState.Stopped,
                            Transition: IpcPlayModeTransition.None,
                            IsPlaying: false,
                            IsPlayingOrWillChangePlaymode: false)),
                    default);
            });
        }

        [TestCase(IpcEditorLifecycleState.Starting, true)]
        [TestCase(IpcEditorLifecycleState.Recovering, true)]
        [TestCase(IpcEditorLifecycleState.Busy, true)]
        [TestCase(IpcEditorLifecycleState.Compiling, true)]
        [TestCase(IpcEditorLifecycleState.DomainReloading, true)]
        [TestCase(IpcEditorLifecycleState.Reimporting, true)]
        [TestCase(IpcEditorLifecycleState.CompileFailed, false)]
        [TestCase(IpcEditorLifecycleState.ModalBlocked, false)]
        [TestCase(IpcEditorLifecycleState.SafeMode, false)]
        [TestCase(IpcEditorLifecycleState.PlayMode, false)]
        [TestCase(IpcEditorLifecycleState.Ready, false)]
        [TestCase(IpcEditorLifecycleState.ShuttingDown, false)]
        [TestCase(IpcEditorLifecycleState.Unavailable, false)]
        [Category("Size.Small")]
        public void IsWaitableState_WhenLifecycleStateMatchesPolicy_ReturnsExpectedValue (
            IpcEditorLifecycleState lifecycleState,
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
                var snapshot = new UnityEditorObservation(
                    state: new UnityEditorStateSnapshot(
                        editorMode: DaemonEditorMode.Batchmode,
                        lifecycleState: testCase.LifecycleState,
                        compileState: IpcCompileState.Ready,
                        generations: new IpcUnityGenerationSnapshot(2, 3, 0, 0),
                        playMode: CreatePlayModeSnapshot(testCase.LifecycleState)),
                    observedAtUtc: DateTimeOffset.UnixEpoch);

                var result = UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot);
                var testCaseName = ContractLiteralCodec.ToValue(testCase.LifecycleState);

                Assert.That(result.IsReady, Is.False, testCaseName);
                Assert.That(result.Observation, Is.EqualTo(snapshot), testCaseName);
                Assert.That(result.Observation.BlockingReason, Is.EqualTo(testCase.BlockingReason), testCaseName);
                Assert.That(
                    IpcEditorLifecycleSemantics.IsConsistent(
                        result.Observation.State.LifecycleState,
                        result.Observation.BlockingReason,
                        result.Observation.CanAcceptExecutionRequests),
                    Is.True,
                    testCaseName);
                Assert.That(result.Error, Is.Not.Null, testCaseName);
                Assert.That(result.Error!.Code, Is.EqualTo(testCase.ExpectedErrorCode), testCaseName);
                Assert.That(result.Error.Message, Is.EqualTo(testCase.ExpectedMessage), testCaseName);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenStartupIsPending_DoesNotConsumeStarting ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                isPlaymodeActive: false,
                out _);

            var first = gate.CaptureObservation();
            var second = gate.CaptureObservation();

            Assert.That(first.State.EditorMode, Is.EqualTo(DaemonEditorMode.Batchmode));
            Assert.That(first.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Starting));
            Assert.That(first.BlockingReason, Is.EqualTo(IpcEditorBlockingReason.Startup));
            Assert.That(first.CanAcceptExecutionRequests, Is.False);
            Assert.That(first.State.Generations.CompileGeneration, Is.EqualTo(4));
            Assert.That(first.State.Generations.DomainReloadGeneration, Is.EqualTo(9));
            Assert.That(second.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Starting));
            Assert.That(second.BlockingReason, Is.EqualTo(IpcEditorBlockingReason.Startup));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenEditorUpdateCompletesStartup_ReturnsReadySnapshot ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true,
                isPlaymodeActive: false,
                out var lifecycleTelemetryState);

            var beforeUpdate = gate.CaptureObservation();
            lifecycleTelemetryState.ObserveEditorUpdate(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            var afterUpdate = gate.CaptureObservation();

            Assert.That(beforeUpdate.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Starting));
            Assert.That(afterUpdate.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(afterUpdate.BlockingReason, Is.Null);
            Assert.That(afterUpdate.CanAcceptExecutionRequests, Is.True);
            Assert.That(
                IpcEditorLifecycleSemantics.IsConsistent(
                    afterUpdate.State.LifecycleState,
                    afterUpdate.BlockingReason,
                    afterUpdate.CanAcceptExecutionRequests),
                Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenEditorUpdateCompletesRecovery_ReturnsReadySnapshot ()
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

            var beforeUpdate = gate.CaptureObservation();
            telemetryState.ObserveEditorUpdate(
                isPlaymodeActive: false,
                isCompiling: false,
                isUpdating: false);
            var afterUpdate = gate.CaptureObservation();

            Assert.That(beforeUpdate.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Recovering));
            Assert.That(beforeUpdate.BlockingReason, Is.EqualTo(IpcEditorBlockingReason.Recovery));
            Assert.That(afterUpdate.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(afterUpdate.BlockingReason, Is.Null);
            Assert.That(afterUpdate.CanAcceptExecutionRequests, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenMutationLaneIsBusy_ReturnsBusyObservation ()
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);
            var gate = new UnityEditorReadinessGate(
                DaemonEditorMode.Gui,
                new UnityEditorLifecycleMonitor(
                    telemetryState,
                    static () => false,
                    static () => false,
                    static () => false,
                    static () => false),
                static () => false,
                new StubMutationExecutionState(isBusy: true),
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);

            var observation = gate.CaptureObservation();

            Assert.That(observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Busy));
            Assert.That(observation.BlockingReason, Is.EqualTo(IpcEditorBlockingReason.Busy));
            Assert.That(observation.CanAcceptExecutionRequests, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void EnsureExecutionReady_WhenAdmittedMutationLaneIsBusy_UsesUnderlyingEditorState ()
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);
            var gate = new UnityEditorReadinessGate(
                DaemonEditorMode.Gui,
                new UnityEditorLifecycleMonitor(
                    telemetryState,
                    static () => false,
                    static () => false,
                    static () => false,
                    static () => false),
                static () => false,
                new StubMutationExecutionState(isBusy: true),
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);

            var readinessTask = gate.EnsureExecutionReadyAsync(failFast: false);

            Assert.That(readinessTask.IsCompleted, Is.True);
            var result = readinessTask.GetAwaiter().GetResult();
            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(result.Observation.CanAcceptExecutionRequests, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenEditorModeIsGui_ReturnsGuiEditorMode ()
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

            var snapshot = gate.CaptureObservation();

            Assert.That(snapshot.State.EditorMode, Is.EqualTo(DaemonEditorMode.Gui));
            Assert.That(snapshot.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenPlaymodeIsActive_ReturnsPlaymodeSnapshot ()
        {
            var gate = CreateGate(
                compileGeneration: 5,
                domainReloadGeneration: 12,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: true,
                out _);

            var snapshot = gate.CaptureObservation();

            Assert.That(snapshot.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.PlayMode));
            Assert.That(snapshot.BlockingReason, Is.EqualTo(IpcEditorBlockingReason.PlayMode));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(snapshot.State.PlayMode, Is.Not.Null);
            Assert.That(snapshot.State.PlayMode.State, Is.EqualTo(IpcPlayModeState.Playing));
            Assert.That(snapshot.State.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransition.None));
            Assert.That(snapshot.State.PlayMode.IsPlaying, Is.True);
            Assert.That(snapshot.State.PlayMode.IsPlayingOrWillChangePlaymode, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenBatchmodeIsStopped_ReturnsStoppedPlayModeSnapshot ()
        {
            var gate = CreateGate(
                compileGeneration: 4,
                domainReloadGeneration: 9,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                out _);

            var snapshot = gate.CaptureObservation();

            Assert.That(snapshot.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(snapshot.State.PlayMode, Is.Not.Null);
            Assert.That(snapshot.State.PlayMode.State, Is.EqualTo(IpcPlayModeState.Stopped));
            Assert.That(snapshot.State.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransition.None));
            Assert.That(snapshot.State.PlayMode.IsPlaying, Is.False);
            Assert.That(snapshot.State.PlayMode.IsPlayingOrWillChangePlaymode, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenPlayModeChangeIsPending_ReturnsPlaymodeEnteringSnapshot ()
        {
            var monitor = CreateMonitor(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: true);

            var snapshot = monitor.CaptureObservation(DaemonEditorMode.Batchmode);

            Assert.That(snapshot.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.PlayMode));
            Assert.That(snapshot.BlockingReason, Is.EqualTo(IpcEditorBlockingReason.PlayMode));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(snapshot.State.PlayMode.State, Is.EqualTo(IpcPlayModeState.Entering));
            Assert.That(snapshot.State.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransition.None));
            Assert.That(snapshot.State.PlayMode.IsPlaying, Is.False);
            Assert.That(snapshot.State.PlayMode.IsPlayingOrWillChangePlaymode, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenPlayModeFlagsAreInconsistent_ReturnsPlaymodeWithUnknownPlayModeState ()
        {
            var monitor = CreateMonitor(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: false);

            var snapshot = monitor.CaptureObservation(DaemonEditorMode.Batchmode);

            Assert.That(snapshot.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.PlayMode));
            Assert.That(snapshot.BlockingReason, Is.EqualTo(IpcEditorBlockingReason.PlayMode));
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.False);
            Assert.That(snapshot.State.PlayMode.State, Is.EqualTo(IpcPlayModeState.Unknown));
            Assert.That(snapshot.State.PlayMode.Transition, Is.EqualTo(IpcPlayModeTransition.None));
            Assert.That(snapshot.State.PlayMode.IsPlaying, Is.True);
            Assert.That(snapshot.State.PlayMode.IsPlayingOrWillChangePlaymode, Is.False);
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
            var enteringGeneration = telemetryState.CaptureGenerationSnapshot().PlayModeGeneration;
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            var exiting = telemetryState.CapturePlayModeSnapshot(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true);
            var exitingGeneration = telemetryState.CaptureGenerationSnapshot().PlayModeGeneration;

            Assert.That(entering.State, Is.EqualTo(IpcPlayModeState.Entering));
            Assert.That(entering.Transition, Is.EqualTo(IpcPlayModeTransition.Entering));
            Assert.That(enteringGeneration, Is.EqualTo(40));
            Assert.That(exiting.State, Is.EqualTo(IpcPlayModeState.Exiting));
            Assert.That(exiting.Transition, Is.EqualTo(IpcPlayModeTransition.Exiting));
            Assert.That(exitingGeneration, Is.EqualTo(40));
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

            _ = telemetryState.CapturePlayModeSnapshot(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false);
            var beforeGeneration = telemetryState.CaptureGenerationSnapshot().PlayModeGeneration;
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.ExitingEditMode);
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
            var entered = telemetryState.CapturePlayModeSnapshot(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true);
            var enteredGeneration = telemetryState.CaptureGenerationSnapshot().PlayModeGeneration;
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            telemetryState.OnPlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            var exited = telemetryState.CapturePlayModeSnapshot(
                isPlaying: false,
                isPlayingOrWillChangePlaymode: false);
            var exitedGeneration = telemetryState.CaptureGenerationSnapshot().PlayModeGeneration;

            Assert.That(beforeGeneration, Is.EqualTo(100));
            Assert.That(entered.State, Is.EqualTo(IpcPlayModeState.Playing));
            Assert.That(entered.Transition, Is.EqualTo(IpcPlayModeTransition.None));
            Assert.That(enteredGeneration, Is.EqualTo(101));
            Assert.That(exited.State, Is.EqualTo(IpcPlayModeState.Stopped));
            Assert.That(exited.Transition, Is.EqualTo(IpcPlayModeTransition.None));
            Assert.That(exitedGeneration, Is.EqualTo(102));
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
            var enteredGeneration = telemetryState.CaptureGenerationSnapshot().PlayModeGeneration;
            _ = telemetryState.CapturePlayModeSnapshot(
                isPlaying: true,
                isPlayingOrWillChangePlaymode: true);
            var repeatedGeneration = telemetryState.CaptureGenerationSnapshot().PlayModeGeneration;

            Assert.That(entered.State, Is.EqualTo(IpcPlayModeState.Playing));
            Assert.That(entered.Transition, Is.EqualTo(IpcPlayModeTransition.None));
            Assert.That(enteredGeneration, Is.EqualTo(201));
            Assert.That(repeatedGeneration, Is.EqualTo(201));
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
            var enteredGeneration = telemetryState.CaptureGenerationSnapshot().PlayModeGeneration;

            Assert.That(entered.State, Is.EqualTo(IpcPlayModeState.Playing));
            Assert.That(enteredGeneration, Is.EqualTo(300));
            Assert.That(UnityEditorSessionStateStore.RestorePlayModeStableState(), Is.EqualTo(IpcPlayModeState.Playing));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureObservation_WhenEditorIsIdle_ReturnsReadySnapshot ()
        {
            var gate = CreateGate(
                compileGeneration: 5,
                domainReloadGeneration: 12,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false,
                isPlaymodeActive: false,
                out _);

            var snapshot = gate.CaptureObservation();

            Assert.That(snapshot.State.EditorMode, Is.EqualTo(DaemonEditorMode.Batchmode));
            Assert.That(snapshot.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(snapshot.BlockingReason, Is.Null);
            Assert.That(snapshot.CanAcceptExecutionRequests, Is.True);
            Assert.That(snapshot.State.Generations.CompileGeneration, Is.EqualTo(5));
            Assert.That(snapshot.State.Generations.DomainReloadGeneration, Is.EqualTo(12));
        }

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
                out var activityProbe,
                out var waitSignalBus);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            activityProbe.IsUpdating = false;
            waitSignalBus.RaiseEditorUpdate();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate default-wait completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(result.Observation.CanAcceptExecutionRequests, Is.True);
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
                out _,
                out var waitSignalBus);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            waitSignalBus.RaiseEditorUpdate();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate startup update completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(result.Observation.CanAcceptExecutionRequests, Is.True);
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
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.PlayMode));
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
            Assert.That(result.Observation.State.EditorMode, Is.EqualTo(DaemonEditorMode.Gui));
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.PlayMode));
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
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.PlayMode));
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
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Compiling));
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
                isCompiling: false,
                isUpdating: false,
                out var lifecycleTelemetryState,
                out _,
                out var waitSignalBus);

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false);
            Assert.That(resultTask.IsCompleted, Is.False);

            lifecycleTelemetryState.SetDomainReloading(false);
            waitSignalBus.RaiseEditorUpdate();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate domain reload completion",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(result.Observation.CanAcceptExecutionRequests, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenCanceledFromBackground_PropagatesCancellationAndDetachesOnMainThread () => UniTask.ToCoroutine(async () =>
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
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
                out _,
                out var waitSignalBus);
            using var cancellationTokenSource = new CancellationTokenSource();

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false, cancellationTokenSource.Token);
            Assert.That(resultTask.IsCompleted, Is.False);

            await TestAwaiter.WaitAsync(
                Task.Run(cancellationTokenSource.Cancel),
                "Background readiness gate cancellation",
                AsyncWaitTimeout);
            _ = await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(
                    resultTask,
                    "Readiness gate cancellation",
                    AsyncWaitTimeout);
            }, "Readiness gate cancellation result", AsyncWaitTimeout);

            Assert.That(waitSignalBus.UnsubscribeCallCount, Is.EqualTo(3));
            Assert.That(waitSignalBus.BeforeAssemblyReloadUnsubscribeThreadId, Is.EqualTo(mainThreadId));
            Assert.That(waitSignalBus.EditorUpdateUnsubscribeThreadId, Is.EqualTo(mainThreadId));
            Assert.That(waitSignalBus.QuittingUnsubscribeThreadId, Is.EqualTo(mainThreadId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenCanceledDuringEventSubscription_CompletesCancellationAndDetachesOnMainThread () => UniTask.ToCoroutine(async () =>
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
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
                out _,
                out var waitSignalBus);
            using var cancellationTokenSource = new CancellationTokenSource();
            waitSignalBus.BeforeAssemblyReloadSubscribed = cancellationTokenSource.Cancel;

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false, cancellationTokenSource.Token);

            Assert.That(resultTask.IsCompleted, Is.True);
            _ = await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(
                async () =>
                {
                    _ = await resultTask;
                },
                "Readiness gate cancellation during event subscription",
                AsyncWaitTimeout);
            Assert.That(waitSignalBus.UnsubscribeCallCount, Is.EqualTo(3));
            Assert.That(waitSignalBus.BeforeAssemblyReloadUnsubscribeThreadId, Is.EqualTo(mainThreadId));
            Assert.That(waitSignalBus.EditorUpdateUnsubscribeThreadId, Is.EqualTo(mainThreadId));
            Assert.That(waitSignalBus.QuittingUnsubscribeThreadId, Is.EqualTo(mainThreadId));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator EnsureExecutionReady_WhenReadinessCompletesBeforeCancellation_ReturnsReadyResult () => UniTask.ToCoroutine(async () =>
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
                out var activityProbe,
                out var waitSignalBus);
            using var cancellationTokenSource = new CancellationTokenSource();

            var resultTask = gate.EnsureExecutionReadyAsync(failFast: false, cancellationTokenSource.Token);
            Assert.That(resultTask.IsCompleted, Is.False);

            activityProbe.IsUpdating = false;
            waitSignalBus.RaiseEditorUpdate();
            cancellationTokenSource.Cancel();
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate result completed before cancellation",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.True);
            Assert.That(waitSignalBus.UnsubscribeCallCount, Is.EqualTo(3));
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
            Assert.That(resultTask.IsCompleted, Is.True);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate assembly reload boundary",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.DomainReloading));
            Assert.That(result.Observation.CanAcceptExecutionRequests, Is.False);
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
            Assert.That(resultTask.IsCompleted, Is.True);
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Readiness gate shutdown boundary",
                AsyncWaitTimeout);

            Assert.That(result.IsReady, Is.False);
            Assert.That(result.Observation.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.ShuttingDown));
            Assert.That(result.Observation.CanAcceptExecutionRequests, Is.False);
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
                new StubMutationExecutionState(isBusy: false),
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);
        }

        private static UnityEditorReadinessGate CreateGate (
            long compileGeneration,
            long domainReloadGeneration,
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
            long compileGeneration,
            long domainReloadGeneration,
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
            long compileGeneration,
            long domainReloadGeneration,
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
                new StubMutationExecutionState(isBusy: false),
                signalBus.SubscribeBeforeAssemblyReload,
                signalBus.UnsubscribeBeforeAssemblyReload,
                signalBus.SubscribeEditorUpdate,
                signalBus.UnsubscribeEditorUpdate,
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
                new StubMutationExecutionState(isBusy: false),
                static _ => { },
                static _ => { },
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

        private static IpcPlayModeSnapshot CreatePlayModeSnapshot (IpcEditorLifecycleState lifecycleState)
        {
            var isPlaying = lifecycleState == IpcEditorLifecycleState.PlayMode;
            return new IpcPlayModeSnapshot(
                State: isPlaying ? IpcPlayModeState.Playing : IpcPlayModeState.Stopped,
                Transition: IpcPlayModeTransition.None,
                IsPlaying: isPlaying,
                IsPlayingOrWillChangePlaymode: isPlaying);
        }

        private sealed class EditorActivityProbe
        {
            public bool IsCompiling { get; set; }

            public bool IsUpdating { get; set; }

            public bool IsPlaymodeActive { get; set; }
        }

        private sealed class StubMutationExecutionState : IUnityMutationExecutionState
        {
            public StubMutationExecutionState (bool isBusy)
            {
                IsBusy = isBusy;
            }

            public bool IsBusy { get; }
        }

        private sealed class WaitSignalBus
        {
            private int unsubscribeCallCount;

            private int beforeAssemblyReloadUnsubscribeThreadId;

            private int editorUpdateUnsubscribeThreadId;

            private int quittingUnsubscribeThreadId;

            private event AssemblyReloadEvents.AssemblyReloadCallback BeforeAssemblyReload;

            private event EditorApplication.CallbackFunction EditorUpdate;

            private event Action Quitting;

            public int UnsubscribeCallCount => Volatile.Read(ref unsubscribeCallCount);

            public int BeforeAssemblyReloadUnsubscribeThreadId =>
                Volatile.Read(ref beforeAssemblyReloadUnsubscribeThreadId);

            public int EditorUpdateUnsubscribeThreadId =>
                Volatile.Read(ref editorUpdateUnsubscribeThreadId);

            public int QuittingUnsubscribeThreadId =>
                Volatile.Read(ref quittingUnsubscribeThreadId);

#nullable enable
            public Action? BeforeAssemblyReloadSubscribed { get; set; }
#nullable restore

            public void SubscribeBeforeAssemblyReload (AssemblyReloadEvents.AssemblyReloadCallback handler)
            {
                BeforeAssemblyReload += handler;
                BeforeAssemblyReloadSubscribed?.Invoke();
            }

            public void UnsubscribeBeforeAssemblyReload (AssemblyReloadEvents.AssemblyReloadCallback handler)
            {
                Volatile.Write(ref beforeAssemblyReloadUnsubscribeThreadId, Thread.CurrentThread.ManagedThreadId);
                Interlocked.Increment(ref unsubscribeCallCount);
                BeforeAssemblyReload -= handler;
            }

            public void SubscribeEditorUpdate (EditorApplication.CallbackFunction handler)
            {
                EditorUpdate += handler;
            }

            public void UnsubscribeEditorUpdate (EditorApplication.CallbackFunction handler)
            {
                Volatile.Write(ref editorUpdateUnsubscribeThreadId, Thread.CurrentThread.ManagedThreadId);
                Interlocked.Increment(ref unsubscribeCallCount);
                EditorUpdate -= handler;
            }

            public void SubscribeQuitting (Action handler)
            {
                Quitting += handler;
            }

            public void UnsubscribeQuitting (Action handler)
            {
                Volatile.Write(ref quittingUnsubscribeThreadId, Thread.CurrentThread.ManagedThreadId);
                Interlocked.Increment(ref unsubscribeCallCount);
                Quitting -= handler;
            }

            public void RaiseBeforeAssemblyReload ()
            {
                BeforeAssemblyReload?.Invoke();
            }

            public void RaiseEditorUpdate ()
            {
                EditorUpdate?.Invoke();
            }

            public void RaiseQuitting ()
            {
                Quitting?.Invoke();
            }
        }
    }
}
