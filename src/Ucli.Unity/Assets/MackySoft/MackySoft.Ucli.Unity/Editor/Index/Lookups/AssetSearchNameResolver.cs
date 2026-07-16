using System;
using System.IO;

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Resolves the non-empty asset name exposed by live and indexed asset search. </summary>
    internal static class AssetSearchNameResolver
    {
        /// <summary> Resolves an asset object name, falling back to its stable path-derived name when needed. </summary>
        public static string Resolve (
            UnityEngine.Object asset,
            string assetPath)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Asset path must not be empty or whitespace.", nameof(assetPath));
            }

            if (!string.IsNullOrWhiteSpace(asset.name))
            {
                return asset.name;
            }

            // Some Unity-generated and planned assets have no object name while their asset path is valid.
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            return string.IsNullOrWhiteSpace(fileName) ? assetPath : fileName;
        }
    }
}
