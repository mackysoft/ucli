using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
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
                return string.Concat(level, " ", normalizedMessage);
            }

            if (message.line > 0 && message.column > 0)
            {
                return string.Concat(file, "(", message.line, ",", message.column, "): ", level, " ", normalizedMessage);
            }

            if (message.line > 0)
            {
                return string.Concat(file, "(", message.line, "): ", level, " ", normalizedMessage);
            }

            return string.Concat(file, ": ", level, " ", normalizedMessage);
        }
    }
}