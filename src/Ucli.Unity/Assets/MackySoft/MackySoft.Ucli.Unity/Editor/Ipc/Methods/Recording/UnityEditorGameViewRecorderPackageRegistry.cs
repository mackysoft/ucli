using System;
using System.Linq;
using MackySoft.Ucli.Contracts.Recording;
using UnityEditor.PackageManager;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>Reads Recorder registration through Unity's Package Manager API.</summary>
    internal sealed class UnityEditorGameViewRecorderPackageRegistry : IGameViewRecorderPackageRegistry
    {
        /// <inheritdoc />
        public bool TryGetRecorderPackageVersion (out string version)
        {
            var package = PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(static item => string.Equals(
                    item.name,
                    GameViewRecorderCompatibilityMetadata.PackageId,
                    StringComparison.Ordinal));
            if (package == null)
            {
                version = string.Empty;
                return false;
            }

            version = package.version;
            return true;
        }
    }
}
