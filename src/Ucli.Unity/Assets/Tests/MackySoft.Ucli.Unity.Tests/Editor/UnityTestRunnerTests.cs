using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
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
                TestMode: TestMode.EditMode,
                BuildTarget: null,
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
    }
}
