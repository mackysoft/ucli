using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

using UnityTestMode = UnityEditor.TestTools.TestRunner.Api.TestMode;
using UnityTestStatus = UnityEditor.TestTools.TestRunner.Api.TestStatus;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityTestRunnerTests
    {
        [Test]
        [Category("Size.Small")]
        public void CreateExecutionSettings_WhenEditModeRequest_DoesNotEnableSynchronousRun ()
        {
            var requestContext = new UnityTestRunRequestContext(
                RunId: "run-id",
                TestPlatform: "editmode",
                TestMode: UnityTestMode.EditMode,
                TargetPlatform: null,
                TestFilter: "^MackySoft\\.Ucli\\.Unity\\.Tests\\.ExecuteRequestIdempotencyCoordinatorTests$",
                TestCategories: new[] { "Size.Small" },
                AssemblyNames: new[] { "MackySoft.Ucli.Unity.Tests.Editor" },
                ResultsXmlPath: "results.xml",
                EditorLogPath: "editor.log",
                ConsoleLogPath: "console.log");

            var executionSettings = UnityTestRunner.CreateExecutionSettings(requestContext);

            Assert.That(executionSettings.runSynchronously, Is.False);
            Assert.That(executionSettings.filters, Has.Length.EqualTo(1));
            Assert.That(executionSettings.filters[0].testMode, Is.EqualTo(UnityTestMode.EditMode));
            Assert.That(executionSettings.filters[0].groupNames, Is.EqualTo(new[] { requestContext.TestFilter }));
            Assert.That(executionSettings.filters[0].categoryNames, Is.EqualTo(requestContext.TestCategories));
            Assert.That(executionSettings.filters[0].assemblyNames, Is.EqualTo(requestContext.AssemblyNames));
        }

        [Test]
        [Category("Size.Small")]
        public void TestRunCallbacks_WhenCaseCallbacksAreReceived_PublishesStartedAndFinishedEntries ()
        {
            var requestContext = CreateRequestContext();
            var progressSink = new CollectingProgressSink();
            var callbacks = new UnityTestRunner.TestRunCallbacks(requestContext, progressSink);
            var test = new StubTestAdaptor(
                id: "test-id",
                name: "SmokeTest.Passes",
                isSuite: false,
                categories: new[] { "smoke" });
            var result = new StubTestResultAdaptor(
                test,
                UnityTestStatus.Passed,
                duration: 0.042d,
                message: string.Empty,
                stackTrace: string.Empty);

            callbacks.TestStarted(test);
            callbacks.TestFinished(result);

            Assert.That(progressSink.Entries, Has.Count.EqualTo(2));
            Assert.That(progressSink.Entries[0].EventName, Is.EqualTo(TestRunProgressEventNames.CaseStarted));
            Assert.That(progressSink.Entries[0].Payload, Is.TypeOf<TestCaseStartedEntry>());
            var started = (TestCaseStartedEntry)progressSink.Entries[0].Payload;
            Assert.That(started.RunId, Is.EqualTo(requestContext.RunId));
            Assert.That(started.TestId, Is.EqualTo("test-id"));
            Assert.That(started.TestName, Is.EqualTo("SmokeTest.Passes"));
            Assert.That(started.TestPlatform, Is.EqualTo("editmode"));
            Assert.That(started.Categories, Is.EqualTo(new[] { "smoke" }));

            Assert.That(progressSink.Entries[1].EventName, Is.EqualTo(TestRunProgressEventNames.CaseFinished));
            Assert.That(progressSink.Entries[1].Payload, Is.TypeOf<TestCaseFinishedEntry>());
            var finished = (TestCaseFinishedEntry)progressSink.Entries[1].Payload;
            Assert.That(finished.RunId, Is.EqualTo(requestContext.RunId));
            Assert.That(finished.TestId, Is.EqualTo("test-id"));
            Assert.That(finished.Result, Is.EqualTo("pass"));
            Assert.That(finished.DurationMilliseconds, Is.EqualTo(42));
            Assert.That(finished.Message, Is.Null);
            Assert.That(finished.StackTrace, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void TestRunCallbacks_WhenSuiteCallbacksAreReceived_DoesNotPublishCaseEntries ()
        {
            var progressSink = new CollectingProgressSink();
            var callbacks = new UnityTestRunner.TestRunCallbacks(CreateRequestContext(), progressSink);
            var suite = new StubTestAdaptor(
                id: "suite-id",
                name: "SmokeTestSuite",
                isSuite: true,
                categories: Array.Empty<string>());
            var result = new StubTestResultAdaptor(
                suite,
                UnityTestStatus.Passed,
                duration: 0d,
                message: string.Empty,
                stackTrace: string.Empty);

            callbacks.TestStarted(suite);
            callbacks.TestFinished(result);

            Assert.That(progressSink.Entries, Is.Empty);
        }

        [Test]
        [Category("Size.Small")]
        public void TestRunCallbacks_WhenProgressFieldsAreLongOrSparse_NormalizesPayload ()
        {
            var progressSink = new CollectingProgressSink();
            var callbacks = new UnityTestRunner.TestRunCallbacks(CreateRequestContext(), progressSink);
            var categories = new List<string> { "smoke", " ", string.Empty };
            for (var i = 0; i < 70; i++)
            {
                categories.Add("category-" + i);
            }

            var longText = new string('x', 8193);
            var test = new StubTestAdaptor(
                id: "test-id",
                name: longText,
                isSuite: false,
                categories: categories.ToArray());
            var result = new StubTestResultAdaptor(
                test,
                UnityTestStatus.Failed,
                duration: 0.001d,
                message: longText,
                stackTrace: longText);

            callbacks.TestStarted(test);
            callbacks.TestFinished(result);

            var started = (TestCaseStartedEntry)progressSink.Entries[0].Payload;
            var finished = (TestCaseFinishedEntry)progressSink.Entries[1].Payload;
            Assert.That(started.TestName, Does.EndWith("...<truncated>"));
            Assert.That(started.Categories, Has.Length.EqualTo(64));
            Assert.That(started.Categories, Does.Not.Contain(string.Empty));
            Assert.That(started.Categories, Does.Not.Contain(" "));
            Assert.That(finished.Message, Does.EndWith("...<truncated>"));
            Assert.That(finished.StackTrace, Does.EndWith("...<truncated>"));
        }

        [Test]
        [Category("Size.Small")]
        public void CreateExecutionSettings_WhenPlayerTargetRequest_SetsTargetPlatform ()
        {
            var requestContext = new UnityTestRunRequestContext(
                RunId: "run-id",
                TestPlatform: "Android",
                TestMode: UnityTestMode.PlayMode,
                TargetPlatform: BuildTarget.Android,
                TestFilter: null,
                TestCategories: new[] { "Size.Small" },
                AssemblyNames: new[] { "MackySoft.Ucli.Unity.Tests.Editor" },
                ResultsXmlPath: "results.xml",
                EditorLogPath: "editor.log",
                ConsoleLogPath: "console.log");

            var executionSettings = UnityTestRunner.CreateExecutionSettings(requestContext);

            Assert.That(executionSettings.filters, Has.Length.EqualTo(1));
            Assert.That(executionSettings.filters[0].testMode, Is.EqualTo(UnityTestMode.PlayMode));
#pragma warning disable CS0618
            Assert.That(executionSettings.filters[0].targetPlatform, Is.EqualTo(BuildTarget.Android));
#pragma warning restore CS0618
        }

        [Test]
        [Category("Size.Small")]
        public void CreateRequestContext_WhenPlayerBuildTargetLiteral_IsMappedToPlayModeAndTargetPlatform ()
        {
            var factory = new UnityTestRunRequestContextFactory();
            var request = new IpcTestRunRequest(
                TestPlatform: TestRunPlatformCodec.ToValue(TestRunPlatform.Player("Android")),
                TestFilter: null,
                TestCategories: new[] { "Size.Small" },
                AssemblyNames: new[] { "MackySoft.Ucli.Unity.Tests.Editor" },
                TestSettingsPath: null,
                ResultsXmlPath: "results.xml",
                EditorLogPath: "editor.log",
                FailFast: false,
                RunId: "run-id");

            var context = factory.Create(request);

            Assert.That(context.TestMode, Is.EqualTo(UnityTestMode.PlayMode));
            Assert.That(context.TargetPlatform, Is.EqualTo(BuildTarget.Android));
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
                AssemblyNames: new[] { "MackySoft.Ucli.Unity.Tests.Editor" },
                ResultsXmlPath: "results.xml",
                EditorLogPath: "editor.log",
                ConsoleLogPath: "console.log");
        }

        private sealed class CollectingProgressSink : IUnityTestRunProgressSink
        {
            public List<(string EventName, object Payload)> Entries { get; } = new List<(string EventName, object Payload)>();

            public void Publish (
                string eventName,
                object payload)
            {
                Entries.Add((eventName, payload));
            }

            public Task FlushAsync (CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        private sealed class StubTestAdaptor : ITestAdaptor
        {
            public StubTestAdaptor (
                string id,
                string name,
                bool isSuite,
                string[] categories)
            {
                Id = id;
                Name = name;
                FullName = "Tests." + name;
                IsSuite = isSuite;
                Categories = categories;
            }

            public string Id { get; }

            public string Name { get; }

            public string FullName { get; }

            public int TestCaseCount => IsSuite ? 1 : 0;

            public bool HasChildren => false;

            public bool IsSuite { get; }

            public IEnumerable<ITestAdaptor> Children => Array.Empty<ITestAdaptor>();

            public ITestAdaptor Parent => null;

            public int TestCaseTimeout => 0;

            public ITypeInfo TypeInfo => null;

            public IMethodInfo Method => null;

            public object[] Arguments => Array.Empty<object>();

            public string[] Categories { get; }

            public bool IsTestAssembly => false;

            public UnityEditor.TestTools.TestRunner.Api.RunState RunState => UnityEditor.TestTools.TestRunner.Api.RunState.Runnable;

            public string Description => string.Empty;

            public string SkipReason => string.Empty;

            public string ParentId => string.Empty;

            public string ParentFullName => string.Empty;

            public string UniqueName => FullName;

            public string ParentUniqueName => string.Empty;

            public int ChildIndex => 0;

            public UnityTestMode TestMode => UnityTestMode.EditMode;
        }

        private sealed class StubTestResultAdaptor : ITestResultAdaptor
        {
            private readonly UnityTestStatus testStatus;

            public StubTestResultAdaptor (
                ITestAdaptor test,
                UnityTestStatus testStatus,
                double duration,
                string message,
                string stackTrace)
            {
                Test = test;
                this.testStatus = testStatus;
                Duration = duration;
                Message = message;
                StackTrace = stackTrace;
            }

            public ITestAdaptor Test { get; }

            public string Name => Test.Name;

            public string FullName => Test.FullName;

            public string ResultState => TestStatus.ToString();

            public UnityTestStatus TestStatus => testStatus;

            public double Duration { get; }

            public DateTime StartTime => DateTime.UnixEpoch;

            public DateTime EndTime => DateTime.UnixEpoch;

            public string Message { get; }

            public string StackTrace { get; }

            public int AssertCount => 0;

            public int FailCount => TestStatus == UnityTestStatus.Failed ? 1 : 0;

            public int PassCount => TestStatus == UnityTestStatus.Passed ? 1 : 0;

            public int SkipCount => TestStatus == UnityTestStatus.Skipped ? 1 : 0;

            public int InconclusiveCount => TestStatus == UnityTestStatus.Inconclusive ? 1 : 0;

            public bool HasChildren => false;

            public IEnumerable<ITestResultAdaptor> Children => Array.Empty<ITestResultAdaptor>();

            public string Output => string.Empty;

            public TNode ToXml ()
            {
                return new TNode("test-case");
            }
        }
    }
}
