using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Execution context passed to <c>ucli.cs.eval</c> entry points. </summary>
    [UcliDescription("Execution context passed to ucli.cs.eval entry points.")]
    public sealed class UcliCsEvalContext
    {
        private readonly List<CsEvalLogEntry> logs = new List<CsEvalLogEntry>();

        private readonly List<CsEvalTouchedResourceDeclaration> touchedResources = new List<CsEvalTouchedResourceDeclaration>();

        private bool declaredNoTouchedResources;

        /// <summary> Records an informational eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records an informational eval log entry.")]
        public void Log ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevelValues.Log, message);
        }

        /// <summary> Records a warning eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records a warning eval log entry.")]
        public void LogWarning ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevelValues.Warning, message);
        }

        /// <summary> Records an error eval log entry. </summary>
        /// <param name="message"> The log message text. </param>
        [UcliDescription("Records an error eval log entry.")]
        public void LogError ([UcliDescription("Log message text.")] string message)
        {
            AddLog(CsEvalLogLevelValues.Error, message);
        }

        /// <summary> Declares that the eval call did not touch Unity resources. </summary>
        [UcliDescription("Declares that the eval call did not touch Unity resources.")]
        public void DeclareNoTouchedResources ()
        {
            if (touchedResources.Count != 0)
            {
                throw new InvalidOperationException("DeclareNoTouchedResources cannot be used after declaring touched resources.");
            }

            declaredNoTouchedResources = true;
        }

        /// <summary> Declares that the eval call touched a project asset. </summary>
        /// <param name="path"> The project-relative asset path. </param>
        [UcliDescription("Declares that the eval call touched a project asset.")]
        public void DeclareTouchedAsset ([UcliDescription("Project-relative asset path.")] string path)
        {
            AddTouchedResource(IpcExecuteTouchedResourceKindNames.Asset, path);
        }

        /// <summary> Declares that the eval call touched a scene asset. </summary>
        /// <param name="path"> The project-relative scene asset path. </param>
        [UcliDescription("Declares that the eval call touched a scene asset.")]
        public void DeclareTouchedScene ([UcliDescription("Project-relative scene asset path.")] string path)
        {
            AddTouchedResource(IpcExecuteTouchedResourceKindNames.Scene, path);
        }

        /// <summary> Declares that the eval call touched a prefab asset. </summary>
        /// <param name="path"> The project-relative prefab asset path. </param>
        [UcliDescription("Declares that the eval call touched a prefab asset.")]
        public void DeclareTouchedPrefab ([UcliDescription("Project-relative prefab asset path.")] string path)
        {
            AddTouchedResource(IpcExecuteTouchedResourceKindNames.Prefab, path);
        }

        /// <summary> Declares that the eval call touched a ProjectSettings asset. </summary>
        /// <param name="path"> The ProjectSettings-relative or project-relative settings path. </param>
        [UcliDescription("Declares that the eval call touched a ProjectSettings asset.")]
        public void DeclareTouchedProjectSettings ([UcliDescription("ProjectSettings-relative or project-relative settings path.")] string path)
        {
            AddTouchedResource(IpcExecuteTouchedResourceKindNames.ProjectSettings, path);
        }

        internal IReadOnlyList<CsEvalLogEntry> Logs => logs;

        internal bool DeclaredNoTouchedResources => declaredNoTouchedResources;

        internal IReadOnlyList<CsEvalTouchedResourceDeclaration> TouchedResources => touchedResources;

        private void AddLog (
            string level,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Log message must not be empty.", nameof(message));
            }

            logs.Add(new CsEvalLogEntry(level, message));
        }

        private void AddTouchedResource (
            string kind,
            string path)
        {
            if (declaredNoTouchedResources)
            {
                throw new InvalidOperationException("Touched resources cannot be declared after DeclareNoTouchedResources.");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Touched resource path must not be empty.", nameof(path));
            }

            touchedResources.Add(new CsEvalTouchedResourceDeclaration(kind, path));
        }
    }
}
