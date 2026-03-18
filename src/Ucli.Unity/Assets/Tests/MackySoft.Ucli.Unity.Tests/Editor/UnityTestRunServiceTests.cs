using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine.TestTools;

using UnityTestStatus = UnityEditor.TestTools.TestRunner.Api.TestStatus;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityTestRunServiceTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenEditorIsWaitingForReadiness_DelaysRunnerUntilReady () => UniTask.ToCoroutine(async () =>
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

            var responseTask = service.Execute(CreateRequest(), CancellationToken.None).AsUniTask();
            await TestAwaiter.WaitAsync(readinessGate.WaitObserved, "Unity test run service readiness wait", SignalWaitTimeout);

            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(runner.CallCount, Is.EqualTo(0));
            Assert.That(resultsWriter.CallCount, Is.EqualTo(0));
            Assert.That(editorLogExporter.CallCount, Is.EqualTo(0));

            readinessGate.Release();
            var response = await TestAwaiter.WaitAsync(responseTask, "Unity test run service response", SignalWaitTimeout);

            Assert.That(response.ExitCode, Is.EqualTo(0));
            Assert.That(runner.CallCount, Is.EqualTo(1));
            Assert.That(resultsWriter.CallCount, Is.EqualTo(1));
            Assert.That(editorLogExporter.CallCount, Is.EqualTo(1));
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
                await service.Execute(CreateRequest(), CancellationToken.None).AsUniTask();
            }, "Invalid Unity test run request without readiness wait", SignalWaitTimeout);

            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(runner.CallCount, Is.EqualTo(0));
            Assert.That(resultsWriter.CallCount, Is.EqualTo(0));
            Assert.That(editorLogExporter.CallCount, Is.EqualTo(0));
        });

        private static IpcTestRunRequest CreateRequest ()
        {
            return new IpcTestRunRequest(
                TestPlatform: IpcTestRunPlatformCodec.EditMode,
                BuildTarget: null,
                TestFilter: null,
                TestCategories: Array.Empty<string>(),
                AssemblyNames: Array.Empty<string>(),
                TestSettingsPath: null,
                ResultsXmlPath: "/tmp/results.xml",
                EditorLogPath: "/tmp/editor.log");
        }

        private static UnityTestRunRequestContext CreateRequestContext ()
        {
            return new UnityTestRunRequestContext(
                TestMode: TestMode.EditMode,
                BuildTarget: null,
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

            public Task<ITestResultAdaptor> Run (
                UnityTestRunRequestContext requestContext,
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
            public int CallCount { get; private set; }

            public Task ExportRange (
                string sourcePath,
                string destinationPath,
                long startOffset,
                long endOffset,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return Task.CompletedTask;
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