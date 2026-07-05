namespace MackySoft.Tests;

/// <summary> Provides helper methods for creating minimal Unity index-input directory structures in tests. </summary>
internal static class UnityIndexInputTestFactory
{
    internal static readonly string ScriptAssemblyPath = Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll");
    internal static readonly string SampleAssetPath = Path.Combine("Assets", "Data", "Spawner.asset");
    internal static readonly string SampleAssetMetaPath = SampleAssetPath + ".meta";

    private const string DefaultScriptAssemblyContent = "initial";
    private const string DefaultPackagesManifestContent = "{ \"dependencies\": {} }";
    private const string DefaultPackagesLockContent = "{ \"dependencies\": {} }";
    private const string DefaultSampleAssetContent = "initial";
    private const string DefaultSampleAssetMetaContent = "guid: initial";

    private static readonly string PackagesManifestPath = Path.Combine("Packages", "manifest.json");
    private static readonly string PackagesLockPath = Path.Combine("Packages", "packages-lock.json");

    internal static void WriteRequiredCoreInputs (TestDirectoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        scope.WriteFile(ScriptAssemblyPath, DefaultScriptAssemblyContent);
        scope.WriteFile(PackagesManifestPath, DefaultPackagesManifestContent);
        scope.WriteFile(PackagesLockPath, DefaultPackagesLockContent);
        scope.CreateDirectory("Assets");
    }

    internal static void WriteRequiredInputsWithSampleAsset (TestDirectoryScope scope)
    {
        WriteRequiredCoreInputs(scope);
        WriteSampleAsset(scope, DefaultSampleAssetContent);
        WriteSampleAssetMeta(scope, DefaultSampleAssetMetaContent);
    }

    internal static string WriteScriptAssembly (TestDirectoryScope scope, string contents)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.WriteFile(ScriptAssemblyPath, contents);
    }

    internal static string WriteSampleAsset (TestDirectoryScope scope, string contents)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.WriteFile(SampleAssetPath, contents);
    }

    internal static string WriteSampleAssetMeta (TestDirectoryScope scope, string contents)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.WriteFile(SampleAssetMetaPath, contents);
    }

    internal static void WriteAssetWithMeta (
        TestDirectoryScope scope,
        string assetRelativePath,
        string assetContents,
        string metaContents)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRelativePath);

        scope.WriteFile(assetRelativePath, assetContents);
        scope.WriteFile(assetRelativePath + ".meta", metaContents);
    }
}
