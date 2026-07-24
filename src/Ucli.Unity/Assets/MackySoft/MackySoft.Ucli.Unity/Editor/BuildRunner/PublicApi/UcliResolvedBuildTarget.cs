using System;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents the resolved build target identity passed to a uCLI build runner. </summary>
    public sealed class UcliResolvedBuildTarget
    {
        /// <summary> Initializes a new instance of the <see cref="UcliResolvedBuildTarget" /> class. </summary>
        /// <param name="stableName"> The uCLI build target stable name. </param>
        /// <param name="unityBuildTarget"> The Unity build target value. </param>
        public UcliResolvedBuildTarget (
            BuildTargetStableName stableName,
            BuildTarget unityBuildTarget)
        {
            if (!TextVocabulary.IsDefined(stableName))
            {
                throw new ArgumentOutOfRangeException(nameof(stableName), stableName, "Build target must be specified.");
            }

            StableName = stableName;
            UnityBuildTarget = unityBuildTarget;
        }

        /// <summary> Gets the uCLI build target stable name. </summary>
        public BuildTargetStableName StableName { get; }

        /// <summary> Gets the Unity build target value used for runner invocation. </summary>
        public BuildTarget UnityBuildTarget { get; }
    }
}
