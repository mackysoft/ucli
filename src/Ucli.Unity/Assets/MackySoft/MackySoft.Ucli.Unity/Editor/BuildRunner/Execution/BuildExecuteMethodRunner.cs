using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Invokes uCLI executeMethod build runner methods through the shared Unity bridge contract. </summary>
    internal sealed class BuildExecuteMethodRunner
    {
        private readonly BuildExecuteMethodResolver resolver;

        /// <summary> Initializes a new instance of the <see cref="BuildExecuteMethodRunner" /> class. </summary>
        public BuildExecuteMethodRunner (BuildExecuteMethodResolver resolver)
        {
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <summary> Invokes the executeMethod runner described by a validated explicit execution request. </summary>
        public BuildExecuteMethodInvocationResult Run (
            BuildRunExecutionRequest.ExplicitExecuteMethod request,
            IpcProjectIdentity projectIdentity,
            UnityBuildResolvedInput resolvedInput,
            IBuildExecuteMethodProgressSink? progressSink)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (projectIdentity == null)
            {
                throw new ArgumentNullException(nameof(projectIdentity));
            }

            if (resolvedInput == null)
            {
                throw new ArgumentNullException(nameof(resolvedInput));
            }

            var resolution = resolver.Resolve(request.RunnerMethod);
            if (!resolution.IsSuccess)
            {
                return Failure(resolution.ErrorCode!, resolution.ErrorMessage!);
            }

            progressSink?.OnRunnerResolved();

            var context = CreateContext(request, projectIdentity, resolvedInput);
            UcliBuildRunnerResult? result;
            try
            {
                UcliBuildRunnerContext.Current = context;
                progressSink?.OnRunnerStarted();
                result = Invoke(resolution.Method!, context);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                return Failure(
                    BuildErrorCodes.BuildExecuteMethodInvocationFailed,
                    "Build executeMethod runner threw an exception.");
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return Failure(
                    BuildErrorCodes.BuildExecuteMethodInvocationFailed,
                    "Build executeMethod runner invocation failed.");
            }
            finally
            {
                UcliBuildRunnerContext.Current = null;
            }

            if (result == null)
            {
                return Failure(
                    BuildErrorCodes.BuildRunnerResultMissing,
                    "Build executeMethod runner returned no UcliBuildRunnerResult.");
            }

            if (!TryCreateRunnerResultArtifact(
                    result,
                    context.OutputDir,
                    out var runnerResult,
                    out var validationCode,
                    out var validationMessage))
            {
                return Failure(
                    validationCode,
                    validationMessage);
            }

            progressSink?.OnRunnerCompleted(runnerResult);
            return BuildExecuteMethodInvocationResult.Success(runnerResult);
        }

        private static UcliBuildRunnerContext CreateContext (
            BuildRunExecutionRequest.ExplicitExecuteMethod request,
            IpcProjectIdentity projectIdentity,
            UnityBuildResolvedInput resolvedInput)
        {
            var scenes = new string[resolvedInput.ScenePaths.Length];
            for (var i = 0; i < resolvedInput.ScenePaths.Length; i++)
            {
                scenes[i] = resolvedInput.ScenePaths[i].Value;
            }

            return new UcliBuildRunnerContext(
                request.RunId,
                projectIdentity.ProjectPath,
                projectIdentity.ProjectFingerprint,
                request.OutputPath,
                request.ProfilePath,
                request.ProfileDigest,
                new UcliResolvedBuildTarget(request.BuildTarget, resolvedInput.UnityBuildTarget),
                scenes,
                new UcliBuildOptions((resolvedInput.Options & BuildOptions.Development) == BuildOptions.Development),
                request.RunnerArguments,
                request.RunnerEnvironmentVariableValues,
                request.RunnerEnvironmentSecretValues);
        }

        private static UcliBuildRunnerResult? Invoke (
            MethodInfo method,
            UcliBuildRunnerContext context)
        {
            var parameters = method.GetParameters();
            var arguments = parameters.Length == 0
                ? null
                : new object[] { context };
            return (UcliBuildRunnerResult?)method.Invoke(null, arguments);
        }

        private static bool TryCreateRunnerResultArtifact (
            UcliBuildRunnerResult result,
            string outputDirectory,
            [NotNullWhen(true)] out IpcBuildRunnerResultArtifact? runnerResult,
            [NotNullWhen(false)] out UcliCode? errorCode,
            [NotNullWhen(false)] out string? errorMessage)
        {
            runnerResult = null;
            var outputs = result.Outputs.Count == 0
                ? Array.Empty<BuildRunnerOutputPath>()
                : new BuildRunnerOutputPath[result.Outputs.Count];
            for (var i = 0; i < result.Outputs.Count; i++)
            {
                var output = result.Outputs[i];
                if (!BuildRunnerOutputSourcePathResolver.TryResolve(
                        outputDirectory,
                        output,
                        out var outputPath,
                        out var sourcePath))
                {
                    errorCode = BuildErrorCodes.BuildOutputPathInvalid;
                    errorMessage = "Build executeMethod runner result output path is invalid.";
                    return false;
                }

                if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                {
                    errorCode = BuildErrorCodes.BuildRunnerResultInvalid;
                    errorMessage = "Build executeMethod runner result output does not exist.";
                    return false;
                }

                outputs[i] = outputPath;
            }

            IpcBuildRunnerResultBuildReport? buildReport = null;
            if (result.BuildReport != null)
            {
                if (!BuildRunnerOutputSourcePathResolver.TryResolve(
                        outputDirectory,
                        result.BuildReport.Path,
                        out var buildReportPath,
                        out _))
                {
                    errorCode = BuildErrorCodes.BuildRunnerResultInvalid;
                    errorMessage = "Build executeMethod runner result buildReport.path is invalid.";
                    return false;
                }

                buildReport = new IpcBuildRunnerResultBuildReport(buildReportPath);
            }

            runnerResult = new IpcBuildRunnerResultArtifact(
                Source: IpcBuildRunnerResultSource.UcliBuildRunnerResult,
                Status: result.Status,
                DurationMilliseconds: result.Summary.DurationMilliseconds,
                ErrorCount: result.Summary.ErrorCount,
                WarningCount: result.Summary.WarningCount,
                Diagnostics: CreateDiagnostics(result.Diagnostics),
                Outputs: outputs,
                BuildReport: buildReport);
            errorCode = null;
            errorMessage = null;
            return true;
        }

        private static IpcBuildRunnerDiagnostic[] CreateDiagnostics (IReadOnlyList<UcliBuildRunnerDiagnostic> diagnostics)
        {
            if (diagnostics.Count == 0)
            {
                return Array.Empty<IpcBuildRunnerDiagnostic>();
            }

            var output = new IpcBuildRunnerDiagnostic[diagnostics.Count];
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                output[i] = new IpcBuildRunnerDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message);
            }

            return output;
        }

        private static BuildExecuteMethodInvocationResult Failure (
            UcliCode code,
            string message)
        {
            return BuildExecuteMethodInvocationResult.Failure(new IpcError(code, message, null));
        }
    }
}
