using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes Unity Test Framework API runs for daemon test-run requests. </summary>
    internal sealed class UnityTestRunner : IUnityTestRunner
    {
        /// <summary> Executes one Unity Test Framework run and returns the result adaptor. </summary>
        /// <param name="requestContext"> The normalized test-run request context. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        /// <returns> The completed test result adaptor. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestContext" /> is <see langword="null" />. </exception>
        public async Task<ITestResultAdaptor> Run (
            UnityTestRunRequestContext requestContext,
            CancellationToken cancellationToken = default)
        {
            if (requestContext == null)
            {
                throw new ArgumentNullException(nameof(requestContext));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var executionSettings = CreateExecutionSettings(requestContext);
            var callbacks = new TestRunCallbacks();
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            var cancellationRegistration = default(CancellationTokenRegistration);
            try
            {
                testRunnerApi.RegisterCallbacks(callbacks);
                var testRunId = testRunnerApi.Execute(executionSettings);
                cancellationRegistration = RegisterCancellation(testRunId, cancellationToken);
                return await callbacks.WaitForCompletion(cancellationToken);
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
            if (requestContext.BuildTarget.HasValue)
            {
                filter.targetPlatform = requestContext.BuildTarget.Value;
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
        private sealed class TestRunCallbacks : ICallbacks
        {
            private readonly TaskCompletionSource<ITestResultAdaptor> completionSource =
                new TaskCompletionSource<ITestResultAdaptor>(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary> Waits for run completion and supports cancellation token. </summary>
            /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
            /// <returns> The completed test result adaptor. </returns>
            public async Task<ITestResultAdaptor> WaitForCompletion (CancellationToken cancellationToken)
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
            }

            /// <summary> Handles test-finished callback. </summary>
            /// <param name="result"> The finished test node result. </param>
            public void TestFinished (ITestResultAdaptor result)
            {
            }
        }
    }
}