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

            var executionSettings = new ExecutionSettings
            {
                filters = new[] { filter },
                runSynchronously = requestContext.TestMode == TestMode.EditMode,
            };

            var callbacks = new TestRunCallbacks();
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                testRunnerApi.RegisterCallbacks(callbacks);
                testRunnerApi.Execute(executionSettings);
                return await callbacks.WaitForCompletion(cancellationToken);
            }
            finally
            {
                testRunnerApi.UnregisterCallbacks(callbacks);
                UnityEngine.Object.DestroyImmediate(testRunnerApi);
            }
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
