using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Testing;
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

            var executionSettings = CreateExecutionSettings(requestContext);
            var callbacks = new TestRunCallbacks(requestContext, progressSink);
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            var cancellationRegistration = default(CancellationTokenRegistration);
            try
            {
                testRunnerApi.RegisterCallbacks(callbacks);
                var testRunId = testRunnerApi.Execute(executionSettings);
                cancellationRegistration = RegisterCancellation(testRunId, cancellationToken);
                return await callbacks.WaitForCompletionAsync(cancellationToken);
            }
            finally
            {
                cancellationRegistration.Dispose();
                testRunnerApi.UnregisterCallbacks(callbacks);
                UnityEngine.Object.DestroyImmediate(testRunnerApi);
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
        /// <returns> The cancellation registration handle. </returns>
        private static CancellationTokenRegistration RegisterCancellation (
            string testRunId,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || string.IsNullOrWhiteSpace(testRunId))
            {
                return default;
            }

            return cancellationToken.Register(static state =>
            {
                var runId = (string)state;
                try
                {
                    TestRunnerApi.CancelTestRun(runId);
                }
                catch (Exception exception)
                {
                    // NOTE:
                    // Cancellation callback cannot propagate exceptions to caller.
                    // Emit diagnostic information and keep cancellation flow non-fatal.
                    Debug.LogWarning($"Unity test run cancel request failed. runId={runId}. {exception.Message}");
                }
            }, testRunId);
        }

        /// <summary> Receives Unity Test Framework callbacks and exposes completion task. </summary>
        internal sealed class TestRunCallbacks : ICallbacks
        {
            private readonly UnityTestRunRequestContext requestContext;
            private readonly IUnityTestRunProgressSink progressSink;

            private readonly TaskCompletionSource<ITestResultAdaptor> completionSource =
                new TaskCompletionSource<ITestResultAdaptor>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary> Initializes a new instance of the <see cref="TestRunCallbacks" /> class. </summary>
            public TestRunCallbacks (
                UnityTestRunRequestContext requestContext,
                IUnityTestRunProgressSink progressSink)
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

            private static string NormalizeResult (TestStatus status)
            {
                return status switch
                {
                    TestStatus.Passed => "pass",
                    TestStatus.Failed => "fail",
                    TestStatus.Skipped => "skipped",
                    TestStatus.Inconclusive => "inconclusive",
                    _ => "inconclusive",
                };
            }
        }
    }
}
