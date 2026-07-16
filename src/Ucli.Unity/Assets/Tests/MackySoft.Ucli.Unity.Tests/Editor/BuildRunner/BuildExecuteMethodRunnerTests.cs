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

        private static readonly string ProfilePath = Path.Combine(ProjectPathTestValues.WorkspaceRoot, "build.ucli.json");
        private static readonly string BuildReportPath = Path.Combine(ProjectPathTestValues.WorkspaceRoot, ".ucli", "build-report.json");
        private static readonly string BuildLogPath = Path.Combine(ProjectPathTestValues.WorkspaceRoot, ".ucli", "build.log");

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
            var projectIdentity = CreateProjectIdentity();

            var result = runner.Run(
                CreateRequest(TypeName + ".ContextualSuccess"),
                projectIdentity,
                CreateResolvedInput(),
                progressSink: null);

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(UcliBuildRunnerContext.Current, Is.Null);
            Assert.That(currentAtInvocation, Is.SameAs(capturedContext));
            Assert.That(capturedContext, Is.Not.Null);
            Assert.That(capturedContext!.RunId, Is.EqualTo(RunId));
            Assert.That(capturedContext.ProfilePath, Is.EqualTo(ProfilePath));
            Assert.That(capturedContext.ProfileDigest, Is.EqualTo(ProfileDigest));
            Assert.That(capturedContext.ProjectPath, Is.EqualTo(projectIdentity.ProjectPath));
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
            Assert.That(result.RunnerResult.Outputs, Has.Count.EqualTo(1));
            Assert.That(result.RunnerResult.Outputs[0].Value, Is.EqualTo("player.txt"));
            Assert.That(result.RunnerResult.BuildReport, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WhenRunnerPathsUsePlatformSeparators_NormalizesIpcPaths ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".PortableRelativePaths"),
                CreateProjectIdentity(),
                CreateResolvedInput(),
                progressSink: null);

            Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
            Assert.That(result.RunnerResult!.Outputs, Has.Count.EqualTo(1));
            Assert.That(result.RunnerResult.Outputs[0].Value, Is.EqualTo("nested/player.txt"));
            Assert.That(result.RunnerResult.BuildReport, Is.Not.Null);
            Assert.That(result.RunnerResult.BuildReport!.Path.Value, Is.EqualTo("reports/build-report.json"));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WhenRunnerOutputPathEscapesOutputDirectory_ReturnsBuildOutputPathInvalid ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".InvalidOutputPath"),
                CreateProjectIdentity(),
                CreateResolvedInput(),
                progressSink: null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildOutputPathInvalid));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WhenRunnerBuildReportPathEscapesOutputDirectory_ReturnsBuildRunnerResultInvalid ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".InvalidBuildReportPath"),
                CreateProjectIdentity(),
                CreateResolvedInput(),
                progressSink: null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error!.Code, Is.EqualTo(BuildErrorCodes.BuildRunnerResultInvalid));
        }

        [Test]
        [Category("Size.Small")]
        public void Run_WithParameterlessMethod_SetsCurrentDuringInvocationAndClearsAfterReturn ()
        {
            var runner = new BuildExecuteMethodRunner(new BuildExecuteMethodResolver());

            var result = runner.Run(
                CreateRequest(TypeName + ".ParameterlessSuccess"),
                CreateProjectIdentity(),
                CreateResolvedInput(),
                progressSink: null);

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
                CreateProjectIdentity(),
                CreateResolvedInput(),
                progressSink: null);

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
                CreateProjectIdentity(),
                CreateResolvedInput(),
                progressSink: null);

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
        public void UcliBuildRunnerResult_WhenSucceededWithoutOutputs_Throws ()
        {
            var exception = Assert.Throws<ArgumentException>(() => new UcliBuildRunnerResult(
                IpcBuildReportResult.Succeeded,
                Array.Empty<string>(),
                new UcliBuildRunnerSummary(0, 0, 0),
                diagnostics: null,
                buildReport: null));

            Assert.That(exception!.ParamName, Is.EqualTo("outputs"));
        }

        [Test]
        [Category("Size.Small")]
        [TestCase("outputs")]
        [TestCase("diagnostics")]
        public void UcliBuildRunnerResult_WhenCollectionContainsNull_Throws (string parameterName)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                if (parameterName == "outputs")
                {
                    _ = new UcliBuildRunnerResult(
                        IpcBuildReportResult.Succeeded,
                        new string[] { null! },
                        new UcliBuildRunnerSummary(0, 0, 0),
                        diagnostics: null,
                        buildReport: null);
                    return;
                }

                _ = new UcliBuildRunnerResult(
                    IpcBuildReportResult.Succeeded,
                    new[] { "player.txt" },
                    new UcliBuildRunnerSummary(0, 0, 0),
                    new UcliBuildRunnerDiagnostic[] { null! },
                    buildReport: null);
            });

            Assert.That(exception!.ParamName, Is.EqualTo(parameterName));
        }

        [Test]
        [Category("Size.Small")]
        public void UcliBuildRunnerResult_WhenSourceCollectionsChange_PreservesConstructionSnapshot ()
        {
            var outputs = new List<string> { "player.txt" };
            var originalDiagnostic = new UcliBuildRunnerDiagnostic(
                "warning",
                UcliDiagnosticSeverity.Warning,
                "Warning message");
            var diagnostics = new List<UcliBuildRunnerDiagnostic> { originalDiagnostic };
            var result = new UcliBuildRunnerResult(
                IpcBuildReportResult.Succeeded,
                outputs,
                new UcliBuildRunnerSummary(0, 0, 1),
                diagnostics,
                buildReport: null);

            outputs[0] = "changed.txt";
            diagnostics.Clear();

            Assert.That(result.Outputs, Is.EqualTo(new[] { "player.txt" }));
            Assert.That(result.Diagnostics, Is.EqualTo(new[] { originalDiagnostic }));
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

        public static UcliBuildRunnerResult PortableRelativePaths (UcliBuildRunnerContext context)
        {
            WriteRunnerOutput(context, "nested/player.txt");
            return UcliBuildRunnerResult.Succeeded(
                new[] { @"nested\player.txt" },
                buildReport: new UcliBuildRunnerBuildReport(@"reports\build-report.json"));
        }

        public static UcliBuildRunnerResult InvalidOutputPath ()
        {
            return UcliBuildRunnerResult.Succeeded(new[] { "../player.txt" });
        }

        public static UcliBuildRunnerResult InvalidBuildReportPath ()
        {
            return UcliBuildRunnerResult.Canceled(
                buildReport: new UcliBuildRunnerBuildReport("../build-report.json"));
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

        private static BuildRunExecutionRequest.ExplicitExecuteMethod CreateRequest (string method)
        {
            var wireRequest = new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: BuildTargetStableName.StandaloneLinux64,
                SceneSource: BuildProfileSceneSource.Explicit,
                ScenePaths: new[] { new SceneAssetPath("Assets/Scenes/Main.unity") },
                Development: true,
                OutputPath: OutputDirectory,
                OutputLayout: null,
                BuildReportPath: BuildReportPath,
                BuildLogPath: BuildLogPath,
                AllowedEditorModes: new[] { DaemonEditorMode.Batchmode },
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.ExecuteMethod,
                ProfileDigest: ProfileDigest,
                UnityBuildProfile: null,
                ProfilePath: ProfilePath,
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
            return (BuildRunExecutionRequest.ExplicitExecuteMethod)BuildRunExecutionRequest.Create(wireRequest);
        }

        private static IpcProjectIdentity CreateProjectIdentity ()
        {
            return new IpcProjectIdentity(
                projectPath: ProjectPathTestValues.WorkspaceUnityProject,
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
