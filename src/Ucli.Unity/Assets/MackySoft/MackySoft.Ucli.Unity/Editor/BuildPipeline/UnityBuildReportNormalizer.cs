using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using UnityEditor.Build.Reporting;

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Normalizes Unity BuildReport objects into uCLI IPC artifacts. </summary>
    internal static class UnityBuildReportNormalizer
    {
        private const int BuildReportSchemaVersion = 1;

        /// <summary> Normalizes one Unity BuildReport. </summary>
        public static IpcBuildReportArtifact Normalize (BuildReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var summary = report.summary;
            return Normalize(new BuildReportSnapshot(
                Result: ToIpcResult(summary.result),
                Target: summary.platform.ToString(),
                OutputPath: summary.outputPath ?? string.Empty,
                Duration: summary.totalTime,
                TotalSizeBytes: checked((long)summary.totalSize),
                ErrorCount: checked((int)summary.totalErrors),
                WarningCount: checked((int)summary.totalWarnings),
                Steps: CreateStepSnapshots(report.steps)));
        }

        /// <summary> Normalizes a BuildReport snapshot. </summary>
        internal static IpcBuildReportArtifact Normalize (BuildReportSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var steps = CreateSteps(snapshot.Steps);
            return new IpcBuildReportArtifact(
                SchemaVersion: BuildReportSchemaVersion,
                Result: ContractLiteralCodec.ToValue(snapshot.Result),
                Target: snapshot.Target ?? string.Empty,
                OutputPath: snapshot.OutputPath ?? string.Empty,
                DurationMilliseconds: ToMilliseconds(snapshot.Duration),
                TotalSizeBytes: snapshot.TotalSizeBytes,
                ErrorCount: snapshot.ErrorCount,
                WarningCount: snapshot.WarningCount,
                Steps: steps,
                Messages: CreateMessages(snapshot.Steps));
        }

        /// <summary> Maps BuildReport result to a log completion reason. </summary>
        public static IpcBuildLogCompletionReason ToCompletionReason (string result)
        {
            return ContractLiteralCodec.TryParse<IpcBuildReportResult>(result, out var parsedResult)
                ? ToCompletionReason(parsedResult)
                : IpcBuildLogCompletionReason.Failed;
        }

        private static IpcBuildLogCompletionReason ToCompletionReason (IpcBuildReportResult result)
        {
            return result switch
            {
                IpcBuildReportResult.Succeeded => IpcBuildLogCompletionReason.Completed,
                IpcBuildReportResult.Canceled => IpcBuildLogCompletionReason.Canceled,
                _ => IpcBuildLogCompletionReason.Failed,
            };
        }

        private static IpcBuildReportResult ToIpcResult (BuildResult result)
        {
            return result switch
            {
                BuildResult.Succeeded => IpcBuildReportResult.Succeeded,
                BuildResult.Failed => IpcBuildReportResult.Failed,
                BuildResult.Cancelled => IpcBuildReportResult.Canceled,
                _ => IpcBuildReportResult.Unknown,
            };
        }

        private static IReadOnlyList<BuildReportStepSnapshot> CreateStepSnapshots (BuildStep[] steps)
        {
            if (steps == null || steps.Length == 0)
            {
                return Array.Empty<BuildReportStepSnapshot>();
            }

            var output = new BuildReportStepSnapshot[steps.Length];
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                output[i] = new BuildReportStepSnapshot(
                    Name: step.name,
                    Duration: step.duration,
                    Depth: step.depth,
                    Messages: CreateMessageSnapshots(step.messages));
            }

            return output;
        }

        private static IReadOnlyList<BuildReportMessageSnapshot> CreateMessageSnapshots (BuildStepMessage[] messages)
        {
            if (messages == null || messages.Length == 0)
            {
                return Array.Empty<BuildReportMessageSnapshot>();
            }

            var output = new BuildReportMessageSnapshot[messages.Length];
            for (var i = 0; i < messages.Length; i++)
            {
                output[i] = new BuildReportMessageSnapshot(
                    Type: messages[i].type.ToString(),
                    Content: messages[i].content);
            }

            return output;
        }

        private static IReadOnlyList<IpcBuildReportStep> CreateSteps (IReadOnlyList<BuildReportStepSnapshot> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return Array.Empty<IpcBuildReportStep>();
            }

            var output = new IpcBuildReportStep[steps.Count];
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                output[i] = new IpcBuildReportStep(
                    Name: step.Name ?? string.Empty,
                    DurationMilliseconds: ToMilliseconds(step.Duration),
                    Depth: step.Depth,
                    MessageCount: step.Messages?.Count ?? 0);
            }

            return output;
        }

        private static IReadOnlyList<IpcBuildReportMessage> CreateMessages (IReadOnlyList<BuildReportStepSnapshot> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                return Array.Empty<IpcBuildReportMessage>();
            }

            var messages = new List<IpcBuildReportMessage>();
            for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
            {
                var stepMessages = steps[stepIndex].Messages;
                if (stepMessages == null)
                {
                    continue;
                }

                for (var messageIndex = 0; messageIndex < stepMessages.Count; messageIndex++)
                {
                    var message = stepMessages[messageIndex];
                    messages.Add(new IpcBuildReportMessage(
                        Type: message.Type ?? string.Empty,
                        Content: message.Content ?? string.Empty));
                }
            }

            return messages;
        }

        private static long ToMilliseconds (TimeSpan value)
        {
            return checked((long)Math.Round(value.TotalMilliseconds, MidpointRounding.AwayFromZero));
        }

        internal sealed record BuildReportSnapshot (
            IpcBuildReportResult Result,
            string Target,
            string OutputPath,
            TimeSpan Duration,
            long TotalSizeBytes,
            int ErrorCount,
            int WarningCount,
            IReadOnlyList<BuildReportStepSnapshot> Steps);

        internal sealed record BuildReportStepSnapshot (
            string Name,
            TimeSpan Duration,
            int Depth,
            IReadOnlyList<BuildReportMessageSnapshot> Messages);

        internal sealed record BuildReportMessageSnapshot (
            string Type,
            string Content);
    }
}
