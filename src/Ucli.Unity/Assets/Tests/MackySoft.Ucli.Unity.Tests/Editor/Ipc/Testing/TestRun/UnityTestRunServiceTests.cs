using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine.TestTools;

using UnityTestMode = UnityEditor.TestTools.TestRunner.Api.TestMode;
using UnityTestStatus = UnityEditor.TestTools.TestRunner.Api.TestStatus;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityTestRunServiceTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenFailFastIsDisabled_DelaysRunnerUntilReady () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var requestContext = CreateRequestContext();
            var runner = new StubUnityTestRunner((_, _) => Task.FromResult<ITestResultAdaptor>(new StubTestResultAdaptor(failCount: 0)));
            var resultsWriter = new SpyUnityTestResultsXmlWriter();
            var editorLogExporter = new SpyEditorLogRangeExporter();
            var service = new UnityTestRunService(
                new StubUnityTestRunRequestContextFactory(_ => requestContext),
                runner,
                resultsWriter,
                editorLogExporter,
                readinessGate);

            var responseTask = service.ExecuteAsync(
                    CreateRequest(failFast: false),
                    cancellationToken: CancellationToken.None)
                .AsUniTask();
            await TestAwaiter.WaitAsync(readinessGate.WaitObserved, "Unity test run service readiness wait", SignalWaitTimeout);

            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.False);
            Assert.That(runner.CallCount, Is.EqualTo(0));
            Assert.That(resultsWriter.CallCount, Is.EqualTo(0));
            Assert.That(editorLogExporter.CallCount, Is.EqualTo(0));

            readinessGate.Release();
            var response = await TestAwaiter.WaitAsync(responseTask, "Unity test run service response", SignalWaitTimeout);

            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Payload!.ExitCode, Is.EqualTo(0));
            Assert.That(runner.CallCount, Is.EqualTo(1));
            Assert.That(resultsWriter.CallCount, Is.EqualTo(1));
            Assert.That(editorLogExporter.CallCount, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhileTestRunIsActive_PublishesRecoveryEditorLogBeforeCompletion () => UniTask.ToCoroutine(async () =>
        {
            var runEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runCompletion = new TaskCompletionSource<ITestResultAdaptor>(TaskCreationOptions.RunContinuationsAsynchronously);
            var requestContext = CreateRequestContext();
            var runner = new StubUnityTestRunner((_, _) =>
            {
                runEntered.TrySetResult(true);
                return runCompletion.Task;
            });
            var editorLogExporter = new SpyEditorLogRangeExporter();
            var service = new UnityTestRunService(
                new StubUnityTestRunRequestContextFactory(_ => requestContext),
                runner,
                new SpyUnityTestResultsXmlWriter(),
                editorLogExporter,
                new StubUnityEditorReadinessGate());

            var responseTask = service.ExecuteAsync(
                CreateRequest(),
                cancellationToken: CancellationToken.None);
            try
            {
                await TestAwaiter.WaitAsync(runEntered.Task, "Unity test run entry", SignalWaitTimeout);

                Assert.That(editorLogExporter.CallCount, Is.EqualTo(1));
                var recoveryExport = editorLogExporter.Invocations[0];
                Assert.That(recoveryExport.SourcePath, Is.EqualTo(requestContext.ConsoleLogPath));
                Assert.That(recoveryExport.DestinationPath, Is.EqualTo(requestContext.EditorLogPath));
                Assert.That(recoveryExport.EndOffset, Is.EqualTo(recoveryExport.StartOffset));
            }
            finally
            {
                runCompletion.TrySetResult(new StubTestResultAdaptor(failCount: 0));
                await TestAwaiter.WaitAsync(responseTask.AsUniTask(), "Unity test run cleanup", SignalWaitTimeout);
            }

            var response = await responseTask;

            Assert.That(response.IsSuccess, Is.True);
            Assert.That(editorLogExporter.CallCount, Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenFailFastIsEnabled_ReturnsLifecycleFailureWithoutRunningTests () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var runner = new StubUnityTestRunner((_, _) => Task.FromResult<ITestResultAdaptor>(new StubTestResultAdaptor(failCount: 0)));
            var resultsWriter = new SpyUnityTestResultsXmlWriter();
            var editorLogExporter = new SpyEditorLogRangeExporter();
            var service = new UnityTestRunService(
                new StubUnityTestRunRequestContextFactory(_ => CreateRequestContext()),
                runner,
                resultsWriter,
                editorLogExporter,
                readinessGate);

            var response = await service.ExecuteAsync(
                    CreateRequest(failFast: true),
                    cancellationToken: CancellationToken.None)
                .AsUniTask();

            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.Error!.Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
            Assert.That(runner.CallCount, Is.EqualTo(0));
            Assert.That(resultsWriter.CallCount, Is.EqualTo(0));
            Assert.That(editorLogExporter.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenRequestIsInvalid_DoesNotWaitForReadiness () => UniTask.ToCoroutine(async () =>
        {
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var runner = new StubUnityTestRunner((_, _) => Task.FromResult<ITestResultAdaptor>(new StubTestResultAdaptor(failCount: 0)));
            var resultsWriter = new SpyUnityTestResultsXmlWriter();
            var editorLogExporter = new SpyEditorLogRangeExporter();
            var service = new UnityTestRunService(
                new StubUnityTestRunRequestContextFactory(_ => throw new ArgumentException("invalid")),
                runner,
                resultsWriter,
                editorLogExporter,
                readinessGate);

            await AsyncExceptionCapture.CaptureAsync<ArgumentException>(async () =>
            {
                await service.ExecuteAsync(
                        CreateRequest(),
                        cancellationToken: CancellationToken.None)
                    .AsUniTask();
            }, "Invalid Unity test run request without readiness wait", SignalWaitTimeout);

            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(runner.CallCount, Is.EqualTo(0));
            Assert.That(resultsWriter.CallCount, Is.EqualTo(0));
            Assert.That(editorLogExporter.CallCount, Is.EqualTo(0));
        });

        private static IpcTestRunRequest CreateRequest (bool failFast = false)
        {
            return new IpcTestRunRequest(
                TestPlatform: TestRunPlatformCodec.EditMode,
                TestFilter: null,
                TestCategories: Array.Empty<string>(),
                AssemblyNames: Array.Empty<string>(),
                TestSettingsPath: null,
                ResultsXmlPath: "/tmp/results.xml",
                EditorLogPath: "/tmp/editor.log",
                FailFast: failFast,
                RunId: "run-id");
        }

        private static UnityTestRunRequestContext CreateRequestContext ()
        {
            return new UnityTestRunRequestContext(
                RunId: "run-id",
                TestPlatform: "editmode",
                TestMode: UnityTestMode.EditMode,
                TargetPlatform: null,
                TestFilter: null,
                TestCategories: Array.Empty<string>(),
                AssemblyNames: Array.Empty<string>(),
                ResultsXmlPath: "/tmp/results.xml",
                EditorLogPath: "/tmp/editor.log",
                ConsoleLogPath: "/tmp/console.log");
        }

        private sealed class StubUnityTestRunRequestContextFactory : IUnityTestRunRequestContextFactory
        {
            private readonly Func<IpcTestRunRequest, UnityTestRunRequestContext> create;

            public StubUnityTestRunRequestContextFactory (Func<IpcTestRunRequest, UnityTestRunRequestContext> create)
            {
                this.create = create;
            }

            public UnityTestRunRequestContext Create (IpcTestRunRequest request)
            {
                return create(request);
            }
        }

        private sealed class StubUnityTestRunner : IUnityTestRunner
        {
            private readonly Func<UnityTestRunRequestContext, CancellationToken, Task<ITestResultAdaptor>> run;

            public StubUnityTestRunner (Func<UnityTestRunRequestContext, CancellationToken, Task<ITestResultAdaptor>> run)
            {
                this.run = run;
            }

            public int CallCount { get; private set; }

            public Task<ITestResultAdaptor> RunAsync (
                UnityTestRunRequestContext requestContext,
                IUnityTestRunProgressSink? progressSink = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return run(requestContext, cancellationToken);
            }
        }

        private sealed class SpyUnityTestResultsXmlWriter : IUnityTestResultsXmlWriter
        {
            public int CallCount { get; private set; }

            public void Write (
                ITestResultAdaptor testResult,
                string resultsXmlPath)
            {
                CallCount++;
            }
        }

        private sealed class SpyEditorLogRangeExporter : IEditorLogRangeExporter
        {
            private readonly List<(string SourcePath, string DestinationPath, long StartOffset, long EndOffset)> invocations =
                new List<(string SourcePath, string DestinationPath, long StartOffset, long EndOffset)>();

            public int CallCount { get; private set; }

            public IReadOnlyList<(string SourcePath, string DestinationPath, long StartOffset, long EndOffset)> Invocations => invocations;

            public Task<EditorLogRangeExportResult> ExportRangeAsync (
                string sourcePath,
                string destinationPath,
                long startOffset,
                long endOffset,
                IEnumerable<string>? redactionValues = null,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                invocations.Add((
                    sourcePath,
                    destinationPath,
                    startOffset,
                    endOffset));
                return Task.FromResult(new EditorLogRangeExportResult(0, 0, 0));
            }
        }

        private sealed class StubTestResultAdaptor : ITestResultAdaptor
        {
            public StubTestResultAdaptor (int failCount)
            {
                FailCount = failCount;
                PassCount = failCount == 0 ? 1 : 0;
            }

            public ITestAdaptor Test => null!;

            public string Name => "test";

            public string FullName => "test";

            public string ResultState => FailCount > 0 ? "Failed" : "Passed";

            public UnityTestStatus TestStatus => FailCount > 0
                ? UnityTestStatus.Failed
                : UnityTestStatus.Passed;

            public double Duration => 0d;

            public DateTime StartTime => DateTime.UnixEpoch;

            public DateTime EndTime => DateTime.UnixEpoch;

            public string Message => string.Empty;

            public string StackTrace => string.Empty;

            public int AssertCount => 0;

            public int FailCount { get; }

            public int PassCount { get; }

            public int SkipCount => 0;

            public int InconclusiveCount => 0;

            public bool HasChildren => false;

            public IEnumerable<ITestResultAdaptor> Children => Array.Empty<ITestResultAdaptor>();

            public string Output => string.Empty;

            public TNode ToXml ()
            {
                return new TNode("test-suite");
            }
        }
    }
}
