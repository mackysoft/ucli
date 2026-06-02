using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Text;
using UnityEditor.Compilation;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Normalizes runtime and compile callbacks into Unity-log stream events. </summary>
    internal sealed class UnityLogCollector
    {
        internal const string DaemonControlPrefix = "[ucli][";

        private readonly IUnityLogStream unityLogStream;

        private readonly UnityCompileMessageDedupeCache compileMessageDedupeCache;

        /// <summary> Initializes a new instance of the <see cref="UnityLogCollector" /> class. </summary>
        /// <param name="unityLogStream"> The Unity-log stream dependency. </param>
        /// <param name="compileMessageDedupeCache"> The compile-message dedupe cache dependency. </param>
        public UnityLogCollector (
            IUnityLogStream unityLogStream,
            UnityCompileMessageDedupeCache compileMessageDedupeCache)
        {
            this.unityLogStream = unityLogStream ?? throw new ArgumentNullException(nameof(unityLogStream));
            this.compileMessageDedupeCache = compileMessageDedupeCache ?? throw new ArgumentNullException(nameof(compileMessageDedupeCache));
        }

        /// <summary> Handles one runtime log callback. </summary>
        /// <param name="condition"> The runtime log message. </param>
        /// <param name="stackTrace"> The runtime stack trace. </param>
        /// <param name="logType"> The Unity runtime log type. </param>
        public void HandleRuntimeLog (
            string condition,
            string stackTrace,
            LogType logType)
        {
            if (!StringValueNormalizer.TryTrimToNonEmpty(condition, out var normalizedCondition))
            {
                return;
            }

            if (normalizedCondition.StartsWith(DaemonControlPrefix, StringComparison.Ordinal)
                || compileMessageDedupeCache.ContainsRecent(normalizedCondition))
            {
                return;
            }

            unityLogStream.Write(
                IpcUnityLogsSourceCodec.Runtime,
                NormalizeLevel(logType),
                normalizedCondition,
                StringValueNormalizer.TrimToNull(stackTrace));
        }

        /// <summary> Handles one compile message callback. </summary>
        /// <param name="message"> The compiler message value. </param>
        public void HandleCompileMessage (CompilerMessage message)
        {
            var normalizedMessage = FormatCompileMessage(message);
            if (string.IsNullOrWhiteSpace(normalizedMessage))
            {
                return;
            }

            compileMessageDedupeCache.Register(normalizedMessage);
            unityLogStream.Write(
                IpcUnityLogsSourceCodec.Compile,
                NormalizeCompileLevel(message.type),
                normalizedMessage,
                null);
        }

        private static string NormalizeLevel (LogType logType)
        {
            switch (logType)
            {
                case LogType.Warning:
                    return IpcDaemonLogsLevelCodec.Warning;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    return IpcDaemonLogsLevelCodec.Error;
                default:
                    return IpcDaemonLogsLevelCodec.Info;
            }
        }

        private static string NormalizeCompileLevel (CompilerMessageType messageType)
        {
            return messageType == CompilerMessageType.Warning
                ? IpcDaemonLogsLevelCodec.Warning
                : IpcDaemonLogsLevelCodec.Error;
        }

        private static string FormatCompileMessage (CompilerMessage message)
        {
            var normalizedMessage = StringValueNormalizer.TrimToNull(message.message);
            if (normalizedMessage == null)
            {
                return string.Empty;
            }

            var level = NormalizeCompileLevel(message.type);
            if (!StringValueNormalizer.TryTrimToNonEmpty(message.file, out var file))
            {
                return CreateCompileMessage(level, normalizedMessage);
            }

            if (message.line > 0 && message.column > 0)
            {
                return CreateCompileMessage(file, message.line, message.column, level, normalizedMessage);
            }

            if (message.line > 0)
            {
                return CreateCompileMessage(file, message.line, level, normalizedMessage);
            }

            return CreateCompileMessage(file, level, normalizedMessage);
        }

        private static string CreateCompileMessage (
            string level,
            string message)
        {
            var length = checked(level.Length + 1 + message.Length);
            return string.Create(
                length,
                (Level: level, Message: message),
                static (destination, state) =>
                {
                    var writer = new SpanTextWriter(destination);
                    writer.Append(state.Level);
                    writer.Append(' ');
                    writer.Append(state.Message);
                });
        }

        private static string CreateCompileMessage (
            string file,
            string level,
            string message)
        {
            var length = checked(file.Length + 2 + level.Length + 1 + message.Length);
            return string.Create(
                length,
                (File: file, Level: level, Message: message),
                static (destination, state) =>
                {
                    var writer = new SpanTextWriter(destination);
                    writer.Append(state.File);
                    writer.Append(": ");
                    writer.Append(state.Level);
                    writer.Append(' ');
                    writer.Append(state.Message);
                });
        }

        private static string CreateCompileMessage (
            string file,
            int line,
            string level,
            string message)
        {
            var lineLength = SpanTextLength.GetInvariantInt64Length(line);
            var length = checked(file.Length + 1 + lineLength + 3 + level.Length + 1 + message.Length);
            return string.Create(
                length,
                (File: file, Line: line, Level: level, Message: message),
                static (destination, state) =>
                {
                    var writer = new SpanTextWriter(destination);
                    writer.Append(state.File);
                    writer.Append('(');
                    writer.AppendInvariant(state.Line);
                    writer.Append("): ");
                    writer.Append(state.Level);
                    writer.Append(' ');
                    writer.Append(state.Message);
                });
        }

        private static string CreateCompileMessage (
            string file,
            int line,
            int column,
            string level,
            string message)
        {
            var lineLength = SpanTextLength.GetInvariantInt64Length(line);
            var columnLength = SpanTextLength.GetInvariantInt64Length(column);
            var length = checked(file.Length + 1 + lineLength + 1 + columnLength + 3 + level.Length + 1 + message.Length);
            return string.Create(
                length,
                (File: file, Line: line, Column: column, Level: level, Message: message),
                static (destination, state) =>
                {
                    var writer = new SpanTextWriter(destination);
                    writer.Append(state.File);
                    writer.Append('(');
                    writer.AppendInvariant(state.Line);
                    writer.Append(',');
                    writer.AppendInvariant(state.Column);
                    writer.Append("): ");
                    writer.Append(state.Level);
                    writer.Append(' ');
                    writer.Append(state.Message);
                });
        }

    }
}
