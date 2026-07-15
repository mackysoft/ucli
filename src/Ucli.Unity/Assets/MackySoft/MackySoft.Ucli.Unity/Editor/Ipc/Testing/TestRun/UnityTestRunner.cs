using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

#nullable enable annotations

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes Unity Test Framework API runs for daemon test-run requests. </summary>
    internal sealed class UnityTestRunner : IUnityTestRunner
    {
        private const int MaxProgressTextLength = 8192;
        private const int MaxProgressCategoryCount = 64;

        private readonly IUnityMutationLaneControl mutationLaneControl;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="UnityTestRunner" /> class. </summary>
        /// <param name="mutationLaneControl"> The mutation safety fence for non-terminal canceled test runs. </param>
        /// <param name="daemonLogger"> The daemon logger used for cancellation failures. </param>
        public UnityTestRunner (
            IUnityMutationLaneControl mutationLaneControl,
            IDaemonLogger daemonLogger)
        {
            this.mutationLaneControl = mutationLaneControl ?? throw new ArgumentNullException(nameof(mutationLaneControl));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <summary> Executes one Unity Test Framework run and returns the result adaptor. </summary>
        /// <param name="requestContext"> The normalized test-run request context. </param>
        /// <param name="progressSink"> The optional sink that receives live test progress entries. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <returns> The completed test result adaptor. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestContext" /> is <see langword="null" />. </exception>
        public async Task<ITestResultAdaptor> RunAsync (
            UnityTestRunRequestContext requestContext,
            IUnityTestRunProgressSink? progressSink = null,
            CancellationToken cancellationToken = default)
        {
            if (requestContext == null)
            {
                throw new ArgumentNullException(nameof(requestContext));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var mainThreadSynchronizationContext = UnityMainThreadGuard.CaptureSynchronizationContext(
                "Unity test runs");

            var executionSettings = CreateExecutionSettings(requestContext);
            var callbacks = new TestRunCallbacks(requestContext, progressSink);
            TestRunnerApi testRunnerApi = null;
            var cancellationRegistration = default(CancellationTokenRegistration);
            IUnityMutationActivity mutationActivity = null;
            Task<ITestResultAdaptor> runCompletionTask = null;
            var deferCleanupUntilRunCompletion = false;
            try
            {
                mutationActivity = mutationLaneControl.BeginMutation();
                testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
                testRunnerApi.RegisterCallbacks(callbacks);
                var testRunId = testRunnerApi.Execute(executionSettings);
                runCompletionTask = callbacks.WaitForCompletionAsync(CancellationToken.None);
                cancellationRegistration = RegisterCancellation(
                    testRunId,
                    cancellationToken,
                    mainThreadSynchronizationContext,
                    daemonLogger,
                    static runId =>
                    {
                        _ = TestRunnerApi.CancelTestRun(runId);
                    });
                try
                {
                    var result = await callbacks.WaitForCompletionAsync(cancellationToken);
                    return result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    deferCleanupUntilRunCompletion = !await AwaitCancellationQuiescenceAsync(
                        callbacks,
                        mutationLaneControl);
                    throw;
                }
            }
            finally
            {
                cancellationRegistration.Dispose();
                if (runCompletionTask != null && !runCompletionTask.IsCompleted)
                {
                    mutationLaneControl.Quarantine(
                        "A Unity test run outlived its request and may still mutate Editor state.",
                        runCompletionTask);
                    deferCleanupUntilRunCompletion = true;
                }

                if (deferCleanupUntilRunCompletion)
                {
                    ScheduleCleanupAfterRunCompletion(
                        runCompletionTask,
                        mainThreadSynchronizationContext,
                        testRunnerApi,
                        callbacks,
                        mutationActivity);
                }
                else
                {
                    try
                    {
                        if (testRunnerApi != null)
                        {
                            CleanupTestRunner(testRunnerApi, callbacks);
                        }
                    }
                    finally
                    {
                        mutationActivity?.Complete();
                    }
                }
            }
        }

        /// <summary> Creates Unity Test Framework execution settings for one daemon test-run request. </summary>
        /// <param name="requestContext"> The normalized request context. </param>
        /// <returns> The execution settings passed to <see cref="TestRunnerApi.Execute(ExecutionSettings)" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestContext" /> is <see langword="null" />. </exception>
        internal static ExecutionSettings CreateExecutionSettings (UnityTestRunRequestContext requestContext)
        {
            if (requestContext == null)
            {
                throw new ArgumentNullException(nameof(requestContext));
            }

            var filter = new Filter
            {
                testMode = requestContext.TestMode,
            };
            if (!string.IsNullOrWhiteSpace(requestContext.TestFilter))
            {
                filter.groupNames = new[] { requestContext.TestFilter };
            }

            if (requestContext.TestCategories.Length > 0)
            {
                filter.categoryNames = requestContext.TestCategories;
            }

            if (requestContext.AssemblyNames.Length > 0)
            {
                filter.assemblyNames = requestContext.AssemblyNames;
            }

#pragma warning disable CS0618
            if (requestContext.TargetPlatform.HasValue)
            {
                filter.targetPlatform = requestContext.TargetPlatform.Value;
            }
#pragma warning restore CS0618

            return new ExecutionSettings
            {
                filters = new[] { filter },
                // NOTE:
                // Daemon requests must stay on the normal asynchronous EditMode execution path.
                // Unity Test Framework admits Task-returning tests into synchronous runs, but that
                // runner cannot drive the yielded Task wrapper and causes false failures or hangs.
                runSynchronously = false,
            };
        }

        /// <summary> Registers Unity Test Framework run-cancel callback for one active run identifier. </summary>
        /// <param name="testRunId"> The active Unity test run identifier returned by <see cref="TestRunnerApi.Execute" />. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <param name="mainThreadSynchronizationContext"> The Unity main-thread context used to dispatch cancellation. </param>
        /// <param name="daemonLogger"> The daemon logger used for a cancellation failure on the Unity main thread. </param>
        /// <param name="cancelTestRun"> The main-thread action that cancels the active Unity test run. </param>
        /// <returns> The cancellation registration handle. </returns>
        internal static CancellationTokenRegistration RegisterCancellation (
            string testRunId,
            CancellationToken cancellationToken,
            SynchronizationContext mainThreadSynchronizationContext,
            IDaemonLogger daemonLogger,
            Action<string> cancelTestRun)
        {
            if (cancelTestRun == null)
            {
                throw new ArgumentNullException(nameof(cancelTestRun));
            }

            if (mainThreadSynchronizationContext == null)
            {
                throw new ArgumentNullException(nameof(mainThreadSynchronizationContext));
            }

            if (daemonLogger == null)
            {
                throw new ArgumentNullException(nameof(daemonLogger));
            }

            if (!cancellationToken.CanBeCanceled || string.IsNullOrWhiteSpace(testRunId))
            {
                return default;
            }

            return cancellationToken.Register(static state =>
            {
                var cancellationState = (TestRunCancellationState)state;
                try
                {
                    cancellationState.MainThreadSynchronizationContext.Post(static postedState =>
                    {
                        var postedCancellationState = (TestRunCancellationState)postedState;
                        try
                        {
                            postedCancellationState.CancelTestRun(postedCancellationState.TestRunId);
                        }
                        catch (Exception exception)
                        {
                            postedCancellationState.DaemonLogger.Warning(
                                DaemonLogCategories.Ipc,
                                $"Unity test run cancel request failed. runId={postedCancellationState.TestRunId}.",
                                exception.ToString());
                        }
                    }, cancellationState);
                }
                catch
                {
                    // Cancellation still detaches the request and quarantines the mutation lane if the
                    // test runner cannot publish RunFinished during Unity synchronization teardown.
                }
            }, new TestRunCancellationState(
                testRunId,
                mainThreadSynchronizationContext,
                daemonLogger,
                cancelTestRun));
        }

        /// <summary> Waits briefly for a canceled Unity test run to terminate, then quarantines the current generation. </summary>
        internal static async Task<bool> AwaitCancellationQuiescenceAsync (
            TestRunCallbacks callbacks,
            IUnityMutationLaneControl mutationLaneControl)
        {
            if (callbacks == null)
            {
                throw new ArgumentNullException(nameof(callbacks));
            }

            if (mutationLaneControl == null)
            {
                throw new ArgumentNullException(nameof(mutationLaneControl));
            }

            var completionTask = callbacks.WaitForCompletionAsync(CancellationToken.None);
            var didQuiesce = await UnityMutationCancellationPolicy.WaitForQuiescenceAsync(completionTask);
            if (didQuiesce)
            {
                await completionTask;
                return true;
            }

            mutationLaneControl.Quarantine(
                "A canceled Unity test run did not publish RunFinished and may still mutate Editor state.",
                completionTask);
            ObserveFault(completionTask);
            return false;
        }

        /// <summary> Defers Unity Test Framework object cleanup until the active run and main-thread cleanup both finish. </summary>
        internal static void ScheduleCleanupAfterRunCompletion (
            Task runCompletionTask,
            SynchronizationContext mainThreadSynchronizationContext,
            TestRunnerApi testRunnerApi,
            TestRunCallbacks callbacks,
            IUnityMutationActivity mutationActivity)
        {
            if (runCompletionTask == null)
            {
                throw new ArgumentNullException(nameof(runCompletionTask));
            }

            ObserveFault(runCompletionTask);
            _ = runCompletionTask.ContinueWith(
                static (_, state) =>
                {
                    var cleanupState = (DeferredTestRunnerCleanupState)state;
                    try
                    {
                        cleanupState.MainThreadSynchronizationContext.Post(
                            static postedState =>
                            {
                                var postedCleanupState = (DeferredTestRunnerCleanupState)postedState;
                                try
                                {
                                    CleanupTestRunner(postedCleanupState.TestRunnerApi, postedCleanupState.Callbacks);
                                }
                                finally
                                {
                                    postedCleanupState.MutationActivity?.Complete();
                                }
                            },
                            cleanupState);
                    }
                    catch
                    {
                        // Unity is tearing down its synchronization context. The completed run is already safe;
                        // the ScriptableObject will be released by Unity lifecycle teardown.
                        cleanupState.MutationActivity?.Complete();
                    }
                },
                new DeferredTestRunnerCleanupState(
                    mainThreadSynchronizationContext,
                    testRunnerApi,
                    callbacks,
                    mutationActivity),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static void CleanupTestRunner (
            TestRunnerApi testRunnerApi,
            TestRunCallbacks callbacks)
        {
            try
            {
                testRunnerApi.UnregisterCallbacks(callbacks);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testRunnerApi);
            }
        }

        private static void ObserveFault (Task task)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private sealed record TestRunCancellationState (
            string TestRunId,
            SynchronizationContext MainThreadSynchronizationContext,
            IDaemonLogger DaemonLogger,
            Action<string> CancelTestRun);

        private sealed record DeferredTestRunnerCleanupState (
            SynchronizationContext MainThreadSynchronizationContext,
            TestRunnerApi TestRunnerApi,
            TestRunCallbacks Callbacks,
            IUnityMutationActivity MutationActivity);

        /// <summary> Receives Unity Test Framework callbacks and exposes completion task. </summary>
        internal sealed class TestRunCallbacks : ICallbacks
        {
            private readonly UnityTestRunRequestContext requestContext;
            private readonly IUnityTestRunProgressSink? progressSink;

            private readonly TaskCompletionSource<ITestResultAdaptor> completionSource =
                new TaskCompletionSource<ITestResultAdaptor>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary> Initializes a new instance of the <see cref="TestRunCallbacks" /> class. </summary>
            public TestRunCallbacks (
                UnityTestRunRequestContext requestContext,
                IUnityTestRunProgressSink? progressSink)
            {
                this.requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
                this.progressSink = progressSink;
            }

            /// <summary> Waits for run completion and supports cancellation token. </summary>
            /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
            /// <returns> The completed test result adaptor. </returns>
            public async Task<ITestResultAdaptor> WaitForCompletionAsync (CancellationToken cancellationToken)
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return await completionSource.Task;
                }

                var cancellationTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var cancellationRegistration = cancellationToken.Register(static state =>
                {
                    var source = (TaskCompletionSource<bool>)state;
                    source.TrySetResult(true);
                }, cancellationTaskSource);

                var completionTask = completionSource.Task;
                var completedTask = await Task.WhenAny(completionTask, cancellationTaskSource.Task);
                if (!ReferenceEquals(completedTask, completionTask))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return await completionTask;
            }

            /// <summary> Handles run-start callback. </summary>
            /// <param name="testsToRun"> The test tree root to run. </param>
            public void RunStarted (ITestAdaptor testsToRun)
            {
            }

            /// <summary> Handles run-finished callback. </summary>
            /// <param name="result"> The completed test run result. </param>
            public void RunFinished (ITestResultAdaptor result)
            {
                completionSource.TrySetResult(result);
            }

            /// <summary> Handles test-start callback. </summary>
            /// <param name="test"> The started test node. </param>
            public void TestStarted (ITestAdaptor test)
            {
                if (!ShouldPublish(test))
                {
                    return;
                }

                progressSink.Publish(
                    TestRunProgressEventNames.CaseStarted,
                    CreateStartedEntry(requestContext, test));
            }

            /// <summary> Handles test-finished callback. </summary>
            /// <param name="result"> The finished test node result. </param>
            public void TestFinished (ITestResultAdaptor result)
            {
                if (result == null || !ShouldPublish(result.Test))
                {
                    return;
                }

                var test = result.Test;
                progressSink.Publish(
                    TestRunProgressEventNames.CaseFinished,
                    new TestCaseFinishedEntry(
                        requestContext.RunId,
                        test.Id,
                        NormalizeProgressText(test.Name),
                        AssemblyName: null,
                        requestContext.TestPlatform,
                        NormalizeCategories(test.Categories),
                        NormalizeResult(result.TestStatus),
                        checked((long)Math.Round(result.Duration * 1000d)),
                        NormalizeOptionalProgressText(result.Message),
                        NormalizeOptionalProgressText(result.StackTrace)));
            }

            private static TestCaseStartedEntry CreateStartedEntry (
                UnityTestRunRequestContext requestContext,
                ITestAdaptor test)
            {
                return new TestCaseStartedEntry(
                    requestContext.RunId,
                    test.Id,
                    NormalizeProgressText(test.Name),
                    AssemblyName: null,
                    requestContext.TestPlatform,
                    NormalizeCategories(test.Categories));
            }

            private bool ShouldPublish (ITestAdaptor test)
            {
                return progressSink != null
                    && test != null
                    && !test.IsSuite;
            }

            private static string[] NormalizeCategories (System.Collections.Generic.IEnumerable<string> categories)
            {
                return categories == null
                    ? Array.Empty<string>()
                    : categories
                        .Where(static category => !string.IsNullOrWhiteSpace(category))
                        .Select(static category => NormalizeProgressText(category))
                        .Take(MaxProgressCategoryCount)
                        .ToArray();
            }

            private static string NormalizeProgressText (string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                return value.Length <= MaxProgressTextLength
                    ? value
                    : value.Substring(0, MaxProgressTextLength) + "...<truncated>";
            }

            private static string NormalizeOptionalProgressText (string value)
            {
                return string.IsNullOrWhiteSpace(value)
                    ? null
                    : NormalizeProgressText(value);
            }

            private static TestCaseResult NormalizeResult (TestStatus status)
            {
                return status switch
                {
                    TestStatus.Passed => TestCaseResult.Pass,
                    TestStatus.Failed => TestCaseResult.Fail,
                    TestStatus.Skipped => TestCaseResult.Skipped,
                    TestStatus.Inconclusive => TestCaseResult.Inconclusive,
                    _ => TestCaseResult.Inconclusive,
                };
            }
        }
    }
}
