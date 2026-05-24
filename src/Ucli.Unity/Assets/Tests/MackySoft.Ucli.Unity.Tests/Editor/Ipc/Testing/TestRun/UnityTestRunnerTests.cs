using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

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
                TestMode: TestMode.EditMode,
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
            Assert.That(executionSettings.filters[0].testMode, Is.EqualTo(TestMode.EditMode));
            Assert.That(executionSettings.filters[0].groupNames, Is.EqualTo(new[] { requestContext.TestFilter }));
            Assert.That(executionSettings.filters[0].categoryNames, Is.EqualTo(requestContext.TestCategories));
            Assert.That(executionSettings.filters[0].assemblyNames, Is.EqualTo(requestContext.AssemblyNames));
        }

        [Test]
        [Category("Size.Small")]
        public void CreateExecutionSettings_WhenPlayerTargetRequest_SetsTargetPlatform ()
        {
            var requestContext = new UnityTestRunRequestContext(
                RunId: "run-id",
                TestPlatform: "Android",
                TestMode: TestMode.PlayMode,
                TargetPlatform: BuildTarget.Android,
                TestFilter: null,
                TestCategories: new[] { "Size.Small" },
                AssemblyNames: new[] { "MackySoft.Ucli.Unity.Tests.Editor" },
                ResultsXmlPath: "results.xml",
                EditorLogPath: "editor.log",
                ConsoleLogPath: "console.log");

            var executionSettings = UnityTestRunner.CreateExecutionSettings(requestContext);

            Assert.That(executionSettings.filters, Has.Length.EqualTo(1));
            Assert.That(executionSettings.filters[0].testMode, Is.EqualTo(TestMode.PlayMode));
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

            Assert.That(context.TestMode, Is.EqualTo(TestMode.PlayMode));
            Assert.That(context.TargetPlatform, Is.EqualTo(BuildTarget.Android));
        }
    }
}
