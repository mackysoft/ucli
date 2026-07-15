using System;
using System.Collections.Generic;
using System.IO;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Build;
using NUnit.Framework;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class BuildExecuteMethodRunnerTests
    {
        private const string TypeName = "MackySoft.Ucli.Unity.Tests.BuildExecuteMethodRunnerTests";
        private static readonly Guid RunId = Guid.Parse("00000000-0000-0000-0000-000000000602");
        private static readonly Sha256Digest ProfileDigest = Sha256Digest.Parse(new string('a', 64));

        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("project-fingerprint");

        private static readonly string OutputDirectory = Path.Combine(
            Path.GetTempPath(),
            "ucli-build-execute-method-runner-tests",
            RunId.ToString("D"),
            "output");

        private static UcliBuildRunnerContext? capturedContext;
        private static UcliBuildRunnerContext? currentAtInvocation;

        [SetUp]
        public void SetUp ()
        {
            capturedContext = null;
            currentAtInvocation = null;
            UcliBuildRunnerContext.Current = null;
            if (Directory.Exists(OutputDirectory))
            {
                Directory.Delete(OutputDirectory, recursive: true);
            }
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
            Assert.That(resolver.Resolve("InternalNestedRunner.Success").IsSuccess, Is.True);
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
        [TestCase("PrivateNestedRunner.Success", "BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE")]
        [TestCase("GenericNestedRunner`1.Success", "BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE")]
        [TestCase(TypeName + ".UnsupportedReturn", "BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE")]
        [TestCase(TypeName + ".UnsupportedParameter", "BUILD_EXECUTE_METHOD_UNSUPPORTED_SIGNATURE")]
        public void Resolve_WithUnsupportedMethod_ReturnsExpectedErrorCode (
            string methodName,
            string expectedCode)
        {
            var result = new BuildExecuteMethodResolver().Resolve(methodName);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode!.Value, Is.EqualTo(expectedCode));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WithContextArgument_PropagatesContextAndClearsCurrent ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".ContextualSuccess"),
                ProfileDigest,
                CreateProjectIdentity(),
                CreateResolvedInput());

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(currentAtInvocation, Is.SameAs(capturedContext));
            Assert.That(capturedContext, Is.Not.Null);
            Assert.That(capturedContext!.RunId, Is.EqualTo(RunId));
            Assert.That(capturedContext.ProfilePath, Is.EqualTo("/workspace/build.ucli.json"));
            Assert.That(capturedContext.ProfileDigest, Is.EqualTo(ProfileDigest));
            Assert.That(capturedContext.ProjectPath, Is.EqualTo("/workspace/UnityProject"));
            Assert.That(capturedContext.ProjectFingerprint, Is.EqualTo(ProjectFingerprint));
            Assert.That(capturedContext.OutputDir, Is.EqualTo(OutputDirectory));
            Assert.That(capturedContext.Target.StableName, Is.EqualTo(BuildTargetStableName.StandaloneLinux64));
            Assert.That(capturedContext.Target.UnityBuildTarget, Is.EqualTo(BuildTarget.StandaloneLinux64));
            Assert.That(capturedContext.Scenes, Is.EqualTo(new[] { "Assets/Scenes/Main.unity" }));
            Assert.That(capturedContext.Options.Development, Is.True);
            Assert.That(capturedContext.Arguments["output"], Is.EqualTo(OutputDirectory));
            Assert.That(capturedContext.Environment.Variables["UCLI_MODE"], Is.EqualTo("release"));
            Assert.That(capturedContext.Environment.Secrets["UCLI_SECRET"], Is.EqualTo("secret-value"));
            Assert.That(result.RunnerResult!.Source, Is.EqualTo(IpcBuildRunnerResultSource.UcliBuildRunnerResult));
            Assert.That(result.RunnerResult.Status, Is.EqualTo(IpcBuildReportResult.Succeeded));
            Assert.That(result.RunnerResult.DurationMilliseconds, Is.EqualTo(1234));
            Assert.That(result.RunnerResult.WarningCount, Is.EqualTo(2));
            Assert.That(result.RunnerResult.Outputs, Is.EqualTo(new[] { "player.txt" }));
            Assert.That(result.RunnerResult.BuildReport, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WithParameterlessMethod_SetsCurrentDuringInvocationAndClearsAfterReturn ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".ParameterlessSuccess"),
                ProfileDigest,
                CreateProjectIdentity(),
                CreateResolvedInput());

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(currentAtInvocation, Is.Not.Null);
            Assert.That(result.RunnerResult!.Status, Is.EqualTo(IpcBuildReportResult.Canceled));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WhenMethodThrows_ReturnsInvocationFailedAndClearsCurrent ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".Throws"),
                ProfileDigest,
                CreateProjectIdentity(),
                CreateResolvedInput());

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildExecuteMethodInvocationFailed));
            Assert.That(result.Error.Message, Does.Not.Contain("secret-value"));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WhenMethodReturnsNull_ReturnsRunnerResultMissingAndClearsCurrent ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".ReturnsNull"),
                ProfileDigest,
                CreateProjectIdentity(),
                CreateResolvedInput());

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildRunnerResultMissing));
        }

        [Test]
        [Category("Size.Small")]
        [TestCase((IpcBuildReportResult)0)]
        [TestCase(IpcBuildReportResult.Unknown)]
        [TestCase((IpcBuildReportResult)999)]
        public void UcliBuildRunnerResult_WhenStatusIsNotTerminal_Throws (IpcBuildReportResult status)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UcliBuildRunnerResult(
                    status,
                    Array.Empty<string>(),
                    new UcliBuildRunnerSummary(0, 0, 0),
                    diagnostics: null,
                    buildReport: null));
        }

        [Test]
        [Category("Size.Small")]
        public void UcliBuildRunnerResult_ConstructorParametersHaveNoDefaultValues ()
        {
            var constructors = typeof(UcliBuildRunnerResult).GetConstructors();
            Assert.That(constructors, Has.Length.EqualTo(1));
            var constructor = constructors[0];

            foreach (var parameter in constructor.GetParameters())
            {
                Assert.That(parameter.IsOptional, Is.False);
                Assert.That(parameter.HasDefaultValue, Is.False);
            }
        }

        public static UcliBuildRunnerResult ContextualSuccess (UcliBuildRunnerContext context)
        {
            capturedContext = context;
            currentAtInvocation = UcliBuildRunnerContext.Current;
            WriteRunnerOutput(context, "player.txt");
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1234, warningCount: 2);
        }

        internal static UcliBuildRunnerResult InternalSuccess ()
        {
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
        }

        public static UcliBuildRunnerResult ParameterlessSuccess ()
        {
            currentAtInvocation = UcliBuildRunnerContext.Current;
            return UcliBuildRunnerResult.Canceled(12, warningCount: 1);
        }

        public static UcliBuildRunnerResult Throws (UcliBuildRunnerContext context)
        {
            throw new InvalidOperationException("runner failed with secret-value");
        }

        public static UcliBuildRunnerResult? ReturnsNull ()
        {
            return null;
        }

        public UcliBuildRunnerResult NonStatic ()
        {
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
        }

        public static UcliBuildRunnerResult Ambiguous ()
        {
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
        }

        public static UcliBuildRunnerResult Ambiguous (UcliBuildRunnerContext context)
        {
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
        }

        public static UcliBuildRunnerResult Generic<T> ()
        {
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
        }

        private static UcliBuildRunnerResult PrivateSuccess ()
        {
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
        }

        internal sealed class InternalNestedRunner
        {
            public static UcliBuildRunnerResult Success ()
            {
                return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
            }
        }

        private sealed class PrivateNestedRunner
        {
            public static UcliBuildRunnerResult Success ()
            {
                return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
            }
        }

        public sealed class GenericNestedRunner<T>
        {
            public static UcliBuildRunnerResult Success ()
            {
                return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
            }
        }

        public static int UnsupportedReturn ()
        {
            return 1;
        }

        public static UcliBuildRunnerResult UnsupportedParameter (string value)
        {
            return UcliBuildRunnerResult.Succeeded(new[] { "player.txt" }, 1);
        }

        private static void WriteRunnerOutput (
            UcliBuildRunnerContext context,
            string relativePath)
        {
            var outputPath = Path.Combine(context.OutputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, "player output");
        }

        private static IpcBuildRunRequest CreateRequest (string method)
        {
            return new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: BuildTargetStableName.StandaloneLinux64,
                SceneSource: BuildProfileSceneSource.Explicit,
                ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/Main.unity") },
                Development: true,
                OutputPath: OutputDirectory,
                OutputLayout: null,
                BuildReportPath: "/workspace/.ucli/build-report.json",
                BuildLogPath: "/workspace/.ucli/build.log",
                AllowedEditorModes: new[] { DaemonEditorMode.Batchmode },
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.ExecuteMethod,
                ProfileDigest: ProfileDigest,
                UnityBuildProfile: null,
                ProfilePath: "/workspace/build.ucli.json",
                RunnerMethod: method,
                RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["output"] = OutputDirectory,
                },
                RunnerEnvironmentVariables: new[] { "UCLI_MODE" },
                RunnerEnvironmentSecrets: new[] { "UCLI_SECRET" },
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UCLI_MODE"] = "release",
                },
                RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UCLI_SECRET"] = "secret-value",
                });
        }

        private static IpcProjectIdentity CreateProjectIdentity ()
        {
            return new IpcProjectIdentity(
                projectPath: "/workspace/UnityProject",
                projectFingerprint: ProjectFingerprint,
                unityVersion: "6000.1.4f1");
        }

        private static UnityBuildResolvedInput CreateResolvedInput ()
        {
            return new UnityBuildResolvedInput(
                UnityBuildTarget: BuildTarget.StandaloneLinux64,
                UnityBuildTargetGroup: BuildTargetGroup.Standalone,
                ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/Main.unity") },
                Options: BuildOptions.Development);
        }
    }
}
