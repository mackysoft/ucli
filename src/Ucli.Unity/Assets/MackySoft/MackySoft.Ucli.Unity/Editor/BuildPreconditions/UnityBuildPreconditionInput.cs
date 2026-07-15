using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents build inputs resolved before Unity BuildPipeline precondition probing. </summary>
    internal sealed record UnityBuildPreconditionInput
    {
        /// <summary> Initializes one resolved build precondition input. </summary>
        public UnityBuildPreconditionInput (
            BuildProfileInputsKind InputKind,
            BuildTargetStableName BuildTarget,
            BuildProfileSceneSource SceneSource,
            IReadOnlyList<SceneAssetPath> ScenePaths,
            bool Development,
            IReadOnlyList<DaemonEditorMode> AllowedEditorModes)
        {
            if (!ContractLiteralCodec.IsDefined(InputKind))
            {
                throw new ArgumentOutOfRangeException(nameof(InputKind), InputKind, "Build input kind must be specified.");
            }

            if (!ContractLiteralCodec.IsDefined(SceneSource))
            {
                throw new ArgumentOutOfRangeException(nameof(SceneSource), SceneSource, "Build scene source must be specified.");
            }

            if (!ContractLiteralCodec.IsDefined(BuildTarget))
            {
                throw new ArgumentOutOfRangeException(nameof(BuildTarget), BuildTarget, "Build target must be specified.");
            }

            this.InputKind = InputKind;
            this.BuildTarget = BuildTarget;
            this.SceneSource = SceneSource;
            this.ScenePaths = ScenePaths ?? throw new ArgumentNullException(nameof(ScenePaths));
            this.Development = Development;
            this.AllowedEditorModes = AllowedEditorModes ?? throw new ArgumentNullException(nameof(AllowedEditorModes));
        }

        public BuildProfileInputsKind InputKind { get; }

        public BuildTargetStableName BuildTarget { get; }

        public BuildProfileSceneSource SceneSource { get; }

        /// <summary> Gets the validated scene asset paths supplied by the selected input source. </summary>
        public IReadOnlyList<SceneAssetPath> ScenePaths { get; }

        public bool Development { get; }

        public IReadOnlyList<DaemonEditorMode> AllowedEditorModes { get; }
    }
}
