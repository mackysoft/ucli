using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Build;
using NUnit.Framework;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class BuildExecuteMethodRunnerTests
    {
        private const string TypeName = "MackySoft.Ucli.Unity.Tests.BuildExecuteMethodRunnerTests";
        private const string RunId = "build-run-1";
        private const string ProjectFingerprint = "project-fingerprint";

        private static UcliBuildRunnerContext? capturedContext;
        private static UcliBuildRunnerContext? currentAtInvocation;

        [SetUp]
        public void SetUp ()
        {
            capturedContext = null;
            currentAtInvocation = null;
            UcliBuildRunnerContext.Current = null;
        }

        [TearDown]
        public void TearDown ()
        {
            UcliBuildRunnerContext.Current = null;
        }

        [Test]
        [Category("Size.Small")]
        public void Resolve_WithSupportedPublicAndInternalStaticMethods_ReturnsSuccess ()
        {
            var resolver = new BuildExecuteMethodResolver();

            Assert.That(resolver.Resolve(TypeName + ".ContextualSuccess").IsSuccess, Is.True);
            Assert.That(resolver.Resolve("BuildExecuteMethodRunnerTests.InternalSuccess").IsSuccess, Is.True);
            Assert.That(resolver.Resolve(TypeName + ".ParameterlessSuccess").IsSuccess, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        [TestCase(TypeName + ".Missing", "BUILD_EXECUTE_METHOD_NOT_FOUND")]
        [TestCase("Run", "BUILD_EXECUTE_METHOD_NOT_FOUND")]
        [TestCase(TypeName + ", MackySoft.Ucli.Unity.Tests.Editor.ContextualSuccess", "BUILD_EXECUTE_METHOD_NOT_FOUND")]
        [TestCase(TypeName + ".NonStatic", "BUILD_EXECUTE_METHOD_NOT_STATIC")]
        [TestCase(TypeName + ".Ambiguous", "BUILD_EXECUTE_METHOD_AMBIGUOUS")]
        [TestCase(TypeName + ".Generic", "BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE")]
        [TestCase(TypeName + ".PrivateSuccess", "BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE")]
        [TestCase(TypeName + ".UnsupportedReturn", "BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE")]
        [TestCase(TypeName + ".UnsupportedParameter", "BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE")]
        public void Resolve_WithUnsupportedMethod_ReturnsExpectedErrorCode (
            string methodName,
            string expectedCode)
        {
            var result = new BuildExecuteMethodResolver().Resolve(methodName);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode!.Value.Value, Is.EqualTo(expectedCode));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WithContextArgument_PropagatesContextAndClearsCurrent ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".ContextualSuccess"),
                CreateProjectIdentity(),
                CreateResolvedInput());

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(currentAtInvocation, Is.SameAs(capturedContext));
            Assert.That(capturedContext, Is.Not.Null);
            Assert.That(capturedContext!.RunId, Is.EqualTo(RunId));
            Assert.That(capturedContext.ProfilePath, Is.EqualTo("/workspace/build.ucli.json"));
            Assert.That(capturedContext.ProfileDigest, Is.EqualTo(new string('a', 64)));
            Assert.That(capturedContext.ProjectPath, Is.EqualTo("/workspace/UnityProject"));
            Assert.That(capturedContext.ProjectFingerprint, Is.EqualTo(ProjectFingerprint));
            Assert.That(capturedContext.Target.StableName, Is.EqualTo("standaloneLinux64"));
            Assert.That(capturedContext.Target.UnityBuildTarget, Is.EqualTo(BuildTarget.StandaloneLinux64));
            Assert.That(capturedContext.Scenes, Is.EqualTo(new[] { "Assets/Scenes/Main.unity" }));
            Assert.That(capturedContext.Options.Development, Is.True);
            Assert.That(capturedContext.Arguments["output"], Is.EqualTo("/workspace/.ucli/output"));
            Assert.That(capturedContext.Environment["UCLI_SECRET"], Is.EqualTo("secret-value"));
            Assert.That(result.RunnerResult!.Source, Is.EqualTo(ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult)));
            Assert.That(result.RunnerResult.Status, Is.EqualTo(ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded)));
            Assert.That(result.RunnerResult.DurationMilliseconds, Is.EqualTo(1234));
            Assert.That(result.RunnerResult.WarningCount, Is.EqualTo(2));
            Assert.That(result.SyntheticReport!.Result, Is.EqualTo(ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded)));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WithParameterlessMethod_SetsCurrentDuringInvocationAndClearsAfterReturn ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".ParameterlessSuccess"),
                CreateProjectIdentity(),
                CreateResolvedInput());

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(currentAtInvocation, Is.Not.Null);
            Assert.That(result.RunnerResult!.Status, Is.EqualTo(ContractLiteralCodec.ToValue(IpcBuildReportResult.Canceled)));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WhenMethodThrows_ReturnsInvocationFailedAndClearsCurrent ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".Throws"),
                CreateProjectIdentity(),
                CreateResolvedInput());

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildExecuteMethodInvocationFailed));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WhenMethodReturnsNull_ReturnsRunnerResultMissingAndClearsCurrent ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".ReturnsNull"),
                CreateProjectIdentity(),
                CreateResolvedInput());

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildRunnerResultMissing));
        }

        public static UcliBuildRunnerResult ContextualSuccess (UcliBuildRunnerContext context)
        {
            capturedContext = context;
            currentAtInvocation = UcliBuildRunnerContext.Current;
            return UcliBuildRunnerResult.Succeeded(1234, warningCount: 2);
        }

        internal static UcliBuildRunnerResult InternalSuccess ()
        {
            return UcliBuildRunnerResult.Succeeded(1);
        }

        public static UcliBuildRunnerResult ParameterlessSuccess ()
        {
            currentAtInvocation = UcliBuildRunnerContext.Current;
            return UcliBuildRunnerResult.Canceled(12, warningCount: 1);
        }

        public static UcliBuildRunnerResult Throws (UcliBuildRunnerContext context)
        {
            throw new InvalidOperationException("runner failed");
        }

        public static UcliBuildRunnerResult? ReturnsNull ()
        {
            return null;
        }

        public UcliBuildRunnerResult NonStatic ()
        {
            return UcliBuildRunnerResult.Succeeded(1);
        }

        public static UcliBuildRunnerResult Ambiguous ()
        {
            return UcliBuildRunnerResult.Succeeded(1);
        }

        public static UcliBuildRunnerResult Ambiguous (UcliBuildRunnerContext context)
        {
            return UcliBuildRunnerResult.Succeeded(1);
        }

        public static UcliBuildRunnerResult Generic<T> ()
        {
            return UcliBuildRunnerResult.Succeeded(1);
        }

        private static UcliBuildRunnerResult PrivateSuccess ()
        {
            return UcliBuildRunnerResult.Succeeded(1);
        }

        public static int UnsupportedReturn ()
        {
            return 1;
        }

        public static UcliBuildRunnerResult UnsupportedParameter (string value)
        {
            return UcliBuildRunnerResult.Succeeded(1);
        }

        private static IpcBuildRunRequest CreateRequest (string method)
        {
            return new IpcBuildRunRequest(
                RunId: RunId,
                BuildTarget: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                SceneSource: "explicit",
                ScenePaths: new[] { "Assets/Scenes/Main.unity" },
                Development: true,
                OutputPath: "/workspace/.ucli/output",
                OutputLayout: null,
                BuildReportPath: "/workspace/.ucli/build-report.json",
                BuildLogPath: "/workspace/.ucli/build.log",
                AllowedEditorModes: new[] { "batchmode" },
                ProjectMutationMode: "forbid")
            {
                RunnerKind = "executeMethod",
                ProfilePath = "/workspace/build.ucli.json",
                ProfileDigest = new string('a', 64),
                RunnerMethod = method,
                RunnerArguments = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["output"] = "/workspace/.ucli/output",
                },
                RunnerEnvironment = new[] { "UCLI_SECRET" },
                RunnerEnvironmentValues = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UCLI_SECRET"] = "secret-value",
                },
            };
        }

        private static IpcProjectIdentity CreateProjectIdentity ()
        {
            return new IpcProjectIdentity(
                ProjectPath: "/workspace/UnityProject",
                ProjectFingerprint: ProjectFingerprint,
                UnityVersion: "6000.1.4f1");
        }

        private static UnityBuildResolvedInput CreateResolvedInput ()
        {
            return new UnityBuildResolvedInput(
                UnityBuildTarget: BuildTarget.StandaloneLinux64,
                UnityBuildTargetGroup: BuildTargetGroup.Standalone,
                ScenePaths: new[] { "Assets/Scenes/Main.unity" },
                Options: BuildOptions.Development);
        }
    }
}
