using System;
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
            UnityBuildResolvedInput resolvedInput)
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

            var context = CreateContext(request, projectIdentity, resolvedInput);
            UcliBuildRunnerResult? result;
            try
            {
                UcliBuildRunnerContext.Current = context;
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

            if (!TryValidateResult(result, out var validationMessage))
            {
                return Failure(
                    BuildErrorCodes.BuildRunnerResultInvalid,
                    validationMessage!);
            }

            var runnerResult = new IpcBuildRunnerResultArtifact(
                Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
                Status: result.Status,
                DurationMilliseconds: result.DurationMilliseconds,
                ErrorCount: result.ErrorCount,
                WarningCount: result.WarningCount,
                Diagnostics: Array.Empty<IpcBuildRunnerDiagnostic>());
            var syntheticReport = new IpcBuildReportArtifact(
                SchemaVersion: 1,
                Result: result.Status,
                UnityBuildTarget: resolvedInput.UnityBuildTarget.ToString(),
                OutputPath: request.OutputPath,
                DurationMilliseconds: result.DurationMilliseconds,
                TotalSizeBytes: 0,
                ErrorCount: result.ErrorCount,
                WarningCount: result.WarningCount,
                Steps: Array.Empty<IpcBuildReportStep>(),
                Messages: Array.Empty<IpcBuildReportMessage>());
            return BuildExecuteMethodInvocationResult.Success(runnerResult, syntheticReport);
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
                request.RunnerEnvironmentValues);
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
            out string? errorMessage)
        {
            if (!ContractLiteralCodec.IsDefined<IpcBuildReportResult>(result.Status)
                || string.Equals(result.Status, ContractLiteralCodec.ToValue(IpcBuildReportResult.Unknown), StringComparison.Ordinal))
            {
                errorMessage = "Build executeMethod runner result status is invalid.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static BuildExecuteMethodInvocationResult Failure (
            UcliCode code,
            string message)
        {
            return BuildExecuteMethodInvocationResult.Failure(new IpcError(code, message, null));
        }
    }
}
