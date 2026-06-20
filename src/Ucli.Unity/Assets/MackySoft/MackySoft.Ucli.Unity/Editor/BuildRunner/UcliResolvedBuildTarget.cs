using System;
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
            string stableName,
            BuildTarget unityBuildTarget)
        {
            if (string.IsNullOrWhiteSpace(stableName))
            {
                throw new ArgumentException("stableName must not be empty.", nameof(stableName));
            }

            StableName = stableName;
            UnityBuildTarget = unityBuildTarget;
        }

        /// <summary> Gets the uCLI build target stable name. </summary>
        public string StableName { get; }

        /// <summary> Gets the Unity build target value used for runner invocation. </summary>
        public BuildTarget UnityBuildTarget { get; }
    }
}
