using System.Diagnostics;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests
{
    public sealed class CliOutputContractTests
    {
        private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(15);

        [Theory]
        [Trait("Size", "Medium")]
        [InlineData(InitCommand.CommandName)]
        [InlineData(StatusCommand.CommandName)]
        public async Task PlaceholderCommand_ReturnsNotImplementedErrorAsSingleJson (string command)
        {
            var result = await RunToolAsync(command);

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
            AssertCommandResultCommon(
                outputJson.RootElement,
                command: command,
                status: CliProtocol.StatusError,
                exitCode: (int)CliExitCode.ToolError);
            AssertSingleError(
                outputJson.RootElement,
                expectedCode: ErrorCodes.CommandNotImplemented);
        }

        [Fact]
        [Trait("Size", "Medium")]
        public async Task Status_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
        {
            var result = await RunToolAsync(StatusCommand.CommandName, "--unknown");

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
            AssertCommandResultCommon(
                outputJson.RootElement,
                command: StatusCommand.CommandName,
                status: CliProtocol.StatusError,
                exitCode: (int)CliExitCode.InvalidArgument);
            AssertSingleError(
                outputJson.RootElement,
                expectedCode: ErrorCodes.InvalidArgument);
            Assert.Contains("Argument '--unknown' is not recognized.", result.StdErr, StringComparison.Ordinal);
        }

        [Fact]
        [Trait("Size", "Medium")]
        public async Task UnknownCommand_ReturnsInvalidArgumentErrorAsSingleJson ()
        {
            var result = await RunToolAsync("unknown");

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
            AssertCommandResultCommon(
                outputJson.RootElement,
                command: CliProtocol.RootCommand,
                status: CliProtocol.StatusError,
                exitCode: (int)CliExitCode.InvalidArgument);
            AssertSingleError(
                outputJson.RootElement,
                expectedCode: ErrorCodes.InvalidArgument);
        }

        private static void AssertCommandResultCommon (
            JsonElement root,
            string command,
            string status,
            int exitCode)
        {
            JsonAssert.For(root)
                .HasInt32("protocolVersion", CliProtocol.CurrentVersion)
                .HasString("command", command)
                .HasString("status", status)
                .HasInt32("exitCode", exitCode)
                .HasValueKind("message", JsonValueKind.String)
                .HasValueKind("payload", JsonValueKind.Object)
                .HasValueKind("errors", JsonValueKind.Array);
        }

        private static void AssertSingleError (JsonElement root, string expectedCode)
        {
            JsonAssert.For(root)
                .HasArrayLength("errors", 1)
                .HasProperty("errors", 0, error => error
                    .HasString("code", expectedCode)
                    .HasValueKind("message", JsonValueKind.String)
                    .IsNull("opId"));
        }

        private static async Task<CommandExecutionResult> RunToolAsync (params string[] args)
        {
            // NOTE:
            // This test validates the process-level CLI contract (stdout JSON, stderr, exit code).
            // Resolving command instances directly would bypass Program and parser error paths.
            var toolPath = typeof(CommandResult).Assembly.Location;
            Assert.True(File.Exists(toolPath), $"CLI assembly was not found: {toolPath}");

            using var process = new Process();
            var startInfo = process.StartInfo;
            startInfo.FileName = "dotnet";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.ArgumentList.Add(toolPath);
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var started = process.Start();
            Assert.True(started, "Failed to start ucli process.");

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            using var timeoutCts = new CancellationTokenSource(ProcessTimeout);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw new TimeoutException($"ucli process timed out after {ProcessTimeout.TotalSeconds} seconds.");
            }

            return new CommandExecutionResult(
                ExitCode: process.ExitCode,
                StdOut: await stdOutTask,
                StdErr: await stdErrTask);
        }

        private readonly record struct CommandExecutionResult (
            int ExitCode,
            string StdOut,
            string StdErr);
    }
}