using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
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

        /// <summary> Invokes the executeMethod runner described by an IPC build.run request. </summary>
        public BuildExecuteMethodInvocationResult Run (
            IpcBuildRunRequest request,
            IpcProjectIdentity projectIdentity,
            UnityBuildResolvedInput resolvedInput,
            IBuildExecuteMethodProgressSink? progressSink = null)
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

            if (string.IsNullOrWhiteSpace(request.RunnerMethod))
            {
                return Failure(
                    BuildErrorCodes.BuildExecuteMethodNotFound,
                    "Build executeMethod runner.method must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(request.ProfilePath) || string.IsNullOrWhiteSpace(request.ProfileDigest))
            {
                return Failure(
                    BuildErrorCodes.BuildRunnerInvocationFailed,
                    "Build executeMethod runner context requires profile path and profile digest.");
            }

            var resolution = resolver.Resolve(request.RunnerMethod);
            if (!resolution.IsSuccess)
            {
                return Failure(resolution.ErrorCode!.Value, resolution.ErrorMessage!);
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

            if (!TryValidateResult(result, context.OutputDir, out var validationCode, out var validationMessage))
            {
                return Failure(
                    validationCode!.Value,
                    validationMessage!);
            }

            var runnerResult = new IpcBuildRunnerResultArtifact(
                Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
                Status: result.Status,
                DurationMilliseconds: result.Summary.DurationMilliseconds,
                ErrorCount: result.Summary.ErrorCount,
                WarningCount: result.Summary.WarningCount,
                Diagnostics: CreateDiagnostics(result.Diagnostics))
            {
                Outputs = CopyOutputs(result.Outputs),
                BuildReport = result.BuildReport == null
                    ? null
                    : new IpcBuildRunnerResultBuildReport(result.BuildReport.Path),
            };
            progressSink?.OnRunnerCompleted(runnerResult);
            return BuildExecuteMethodInvocationResult.Success(runnerResult);
        }

        private static UcliBuildRunnerContext CreateContext (
            IpcBuildRunRequest request,
            IpcProjectIdentity projectIdentity,
            UnityBuildResolvedInput resolvedInput)
        {
            return new UcliBuildRunnerContext(
                request.RunId,
                projectIdentity.ProjectPath,
                projectIdentity.ProjectFingerprint,
                request.OutputPath,
                request.ProfilePath!,
                request.ProfileDigest!,
                new UcliResolvedBuildTarget(request.BuildTarget, resolvedInput.UnityBuildTarget),
                resolvedInput.ScenePaths,
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

        private static bool TryValidateResult (
            UcliBuildRunnerResult result,
            string outputDirectory,
            out UcliCode? errorCode,
            out string? errorMessage)
        {
            if (!ContractLiteralCodec.TryParse<IpcBuildReportResult>(result.Status, out var status)
                || status == IpcBuildReportResult.Unknown)
            {
                errorCode = BuildErrorCodes.BuildRunnerResultInvalid;
                errorMessage = "Build executeMethod runner result status is invalid.";
                return false;
            }

            if (result.Summary.DurationMilliseconds < 0
                || result.Summary.ErrorCount < 0
                || result.Summary.WarningCount < 0)
            {
                errorCode = BuildErrorCodes.BuildRunnerResultInvalid;
                errorMessage = "Build executeMethod runner result summary is invalid.";
                return false;
            }

            if (!HasValidDiagnostics(result.Diagnostics))
            {
                errorCode = BuildErrorCodes.BuildRunnerResultInvalid;
                errorMessage = "Build executeMethod runner result diagnostics are invalid.";
                return false;
            }

            if (status == IpcBuildReportResult.Succeeded && result.Outputs.Count == 0)
            {
                errorCode = BuildErrorCodes.BuildRunnerResultInvalid;
                errorMessage = "Build executeMethod runner result requires at least one output when status is succeeded.";
                return false;
            }

            for (var i = 0; i < result.Outputs.Count; i++)
            {
                var output = result.Outputs[i];
                if (!BuildRunnerOutputSourcePathResolver.TryResolve(outputDirectory, output, out var sourcePath))
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
            }

            if (result.BuildReport != null
                && !BuildRunnerOutputSourcePathResolver.TryResolve(outputDirectory, result.BuildReport.Path, out _))
            {
                errorCode = BuildErrorCodes.BuildRunnerResultInvalid;
                errorMessage = "Build executeMethod runner result buildReport.path is invalid.";
                return false;
            }

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

        private static string[] CopyOutputs (IReadOnlyList<string> outputs)
        {
            if (outputs.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[outputs.Count];
            for (var i = 0; i < outputs.Count; i++)
            {
                copy[i] = outputs[i];
            }

            return copy;
        }

        private static bool HasValidDiagnostics (IReadOnlyList<UcliBuildRunnerDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return false;
            }

            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic == null
                    || string.IsNullOrWhiteSpace(diagnostic.Code)
                    || !IsKnownDiagnosticSeverity(diagnostic.Severity)
                    || string.IsNullOrWhiteSpace(diagnostic.Message))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsKnownDiagnosticSeverity (string severity)
        {
            return severity is IpcExecuteDiagnosticSeverityNames.Info
                or IpcExecuteDiagnosticSeverityNames.Warning
                or IpcExecuteDiagnosticSeverityNames.Error;
        }

        private static BuildExecuteMethodInvocationResult Failure (
            UcliCode code,
            string message)
        {
            return BuildExecuteMethodInvocationResult.Failure(new IpcError(code, message, null));
        }
    }
}
