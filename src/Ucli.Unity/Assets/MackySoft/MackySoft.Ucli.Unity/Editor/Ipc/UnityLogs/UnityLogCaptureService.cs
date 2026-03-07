using System;
using UnityEditor.Compilation;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Subscribes Unity callbacks and forwards them into <see cref="UnityLogCollector" />. </summary>
    internal sealed class UnityLogCaptureService : IDisposable
    {
        private readonly UnityLogCollector unityLogCollector;

        private bool isStarted;

        /// <summary> Initializes a new instance of the <see cref="UnityLogCaptureService" /> class. </summary>
        /// <param name="unityLogCollector"> The Unity-log collector dependency. </param>
        public UnityLogCaptureService (UnityLogCollector unityLogCollector)
        {
            this.unityLogCollector = unityLogCollector ?? throw new ArgumentNullException(nameof(unityLogCollector));
        }

        /// <summary> Starts callback subscriptions. </summary>
        public void Start ()
        {
            if (isStarted)
            {
                return;
            }

            Application.logMessageReceivedThreaded += OnRuntimeLogReceived;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            isStarted = true;
        }

        /// <inheritdoc />
        public void Dispose ()
        {
            if (!isStarted)
            {
                return;
            }

            Application.logMessageReceivedThreaded -= OnRuntimeLogReceived;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            isStarted = false;
        }

        private void OnRuntimeLogReceived (
            string condition,
            string stackTrace,
            LogType logType)
        {
            unityLogCollector.HandleRuntimeLog(condition, stackTrace, logType);
        }

        private void OnAssemblyCompilationFinished (
            string assemblyPath,
            CompilerMessage[] messages)
        {
            if (messages == null)
            {
                return;
            }

            foreach (var message in messages)
            {
                unityLogCollector.HandleCompileMessage(message);
            }
        }
    }
}
