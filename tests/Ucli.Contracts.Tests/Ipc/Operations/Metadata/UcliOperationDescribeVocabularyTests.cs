using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationDescribeVocabularyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ConstraintKindEnum_HasStableValues ()
    {
        Assert.Equal(0, (int)UcliOperationInputConstraintKind.NonEmpty);
        Assert.Equal(1, (int)UcliOperationInputConstraintKind.Range);
        Assert.Equal(2, (int)UcliOperationInputConstraintKind.ProjectRelativePath);
        Assert.Equal(3, (int)UcliOperationInputConstraintKind.AssetExists);
        Assert.Equal(4, (int)UcliOperationInputConstraintKind.AssetCreatable);
        Assert.Equal(5, (int)UcliOperationInputConstraintKind.GlobalObjectId);
        Assert.Equal(6, (int)UcliOperationInputConstraintKind.HierarchyPath);
        Assert.Equal(7, (int)UcliOperationInputConstraintKind.ReferenceResolvable);
        Assert.Equal(8, (int)UcliOperationInputConstraintKind.TypeExists);
        Assert.Equal(9, (int)UcliOperationInputConstraintKind.TypeAssignableTo);
        Assert.Equal(10, (int)UcliOperationInputConstraintKind.SerializedProperty);
        Assert.Equal(11, (int)UcliOperationInputConstraintKind.AssetGuid);
        Assert.Equal(12, (int)UcliOperationInputConstraintKind.Cursor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConstraintKindCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal(UcliOperationInputConstraintKindValues.NonEmpty, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.NonEmpty));
        Assert.Equal(UcliOperationInputConstraintKindValues.Range, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.Range));
        Assert.Equal(UcliOperationInputConstraintKindValues.ProjectRelativePath, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.ProjectRelativePath));
        Assert.Equal(UcliOperationInputConstraintKindValues.AssetExists, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.AssetExists));
        Assert.Equal(UcliOperationInputConstraintKindValues.AssetCreatable, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.AssetCreatable));
        Assert.Equal(UcliOperationInputConstraintKindValues.GlobalObjectId, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.GlobalObjectId));
        Assert.Equal(UcliOperationInputConstraintKindValues.HierarchyPath, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.HierarchyPath));
        Assert.Equal(UcliOperationInputConstraintKindValues.ReferenceResolvable, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.ReferenceResolvable));
        Assert.Equal(UcliOperationInputConstraintKindValues.TypeExists, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.TypeExists));
        Assert.Equal(UcliOperationInputConstraintKindValues.TypeAssignableTo, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.TypeAssignableTo));
        Assert.Equal(UcliOperationInputConstraintKindValues.SerializedProperty, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.SerializedProperty));
        Assert.Equal(UcliOperationInputConstraintKindValues.AssetGuid, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.AssetGuid));
        Assert.Equal(UcliOperationInputConstraintKindValues.Cursor, UcliOperationInputConstraintKindCodec.ToValue(UcliOperationInputConstraintKind.Cursor));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConstraintKindValues_AreStableLiterals ()
    {
        Assert.Equal("nonEmpty", UcliOperationInputConstraintKindValues.NonEmpty);
        Assert.Equal("range", UcliOperationInputConstraintKindValues.Range);
        Assert.Equal("projectRelativePath", UcliOperationInputConstraintKindValues.ProjectRelativePath);
        Assert.Equal("assetExists", UcliOperationInputConstraintKindValues.AssetExists);
        Assert.Equal("assetCreatable", UcliOperationInputConstraintKindValues.AssetCreatable);
        Assert.Equal("globalObjectId", UcliOperationInputConstraintKindValues.GlobalObjectId);
        Assert.Equal("hierarchyPath", UcliOperationInputConstraintKindValues.HierarchyPath);
        Assert.Equal("referenceResolvable", UcliOperationInputConstraintKindValues.ReferenceResolvable);
        Assert.Equal("typeExists", UcliOperationInputConstraintKindValues.TypeExists);
        Assert.Equal("typeAssignableTo", UcliOperationInputConstraintKindValues.TypeAssignableTo);
        Assert.Equal("serializedProperty", UcliOperationInputConstraintKindValues.SerializedProperty);
        Assert.Equal("assetGuid", UcliOperationInputConstraintKindValues.AssetGuid);
        Assert.Equal("cursor", UcliOperationInputConstraintKindValues.Cursor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConstraintParameterEnums_HaveStableValues ()
    {
        Assert.Equal(0, (int)UcliOperationAssetKind.Unspecified);
        Assert.Equal(1, (int)UcliOperationAssetKind.Asset);
        Assert.Equal(2, (int)UcliOperationAssetKind.Prefab);
        Assert.Equal(3, (int)UcliOperationAssetKind.ProjectSettings);
        Assert.Equal(4, (int)UcliOperationAssetKind.Scene);
        Assert.Equal(0, (int)UcliOperationReferenceTargetKind.Unspecified);
        Assert.Equal(1, (int)UcliOperationReferenceTargetKind.Asset);
        Assert.Equal(2, (int)UcliOperationReferenceTargetKind.Component);
        Assert.Equal(3, (int)UcliOperationReferenceTargetKind.GameObject);
        Assert.Equal(0, (int)UcliOperationTypeKind.Unspecified);
        Assert.Equal(1, (int)UcliOperationTypeKind.Component);
        Assert.Equal(0, (int)UcliOperationSerializedPropertyAccess.Unspecified);
        Assert.Equal(1, (int)UcliOperationSerializedPropertyAccess.Write);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConstraintParameterCodecs_ToValue_ReturnStableLiterals ()
    {
        Assert.Equal(UcliOperationAssetKindValues.Asset, UcliOperationAssetKindCodec.ToValue(UcliOperationAssetKind.Asset));
        Assert.Equal(UcliOperationAssetKindValues.Prefab, UcliOperationAssetKindCodec.ToValue(UcliOperationAssetKind.Prefab));
        Assert.Equal(UcliOperationAssetKindValues.ProjectSettings, UcliOperationAssetKindCodec.ToValue(UcliOperationAssetKind.ProjectSettings));
        Assert.Equal(UcliOperationAssetKindValues.Scene, UcliOperationAssetKindCodec.ToValue(UcliOperationAssetKind.Scene));
        Assert.Equal(UcliOperationReferenceTargetKindValues.Asset, UcliOperationReferenceTargetKindCodec.ToValue(UcliOperationReferenceTargetKind.Asset));
        Assert.Equal(UcliOperationReferenceTargetKindValues.Component, UcliOperationReferenceTargetKindCodec.ToValue(UcliOperationReferenceTargetKind.Component));
        Assert.Equal(UcliOperationReferenceTargetKindValues.GameObject, UcliOperationReferenceTargetKindCodec.ToValue(UcliOperationReferenceTargetKind.GameObject));
        Assert.Equal(UcliOperationTypeKindValues.Component, UcliOperationTypeKindCodec.ToValue(UcliOperationTypeKind.Component));
        Assert.Equal(UcliOperationSerializedPropertyAccessValues.Write, UcliOperationSerializedPropertyAccessCodec.ToValue(UcliOperationSerializedPropertyAccess.Write));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConstraintParameterValues_AreStableLiterals ()
    {
        Assert.Equal("asset", UcliOperationAssetKindValues.Asset);
        Assert.Equal("prefab", UcliOperationAssetKindValues.Prefab);
        Assert.Equal("projectSettings", UcliOperationAssetKindValues.ProjectSettings);
        Assert.Equal("scene", UcliOperationAssetKindValues.Scene);
        Assert.Equal("asset", UcliOperationReferenceTargetKindValues.Asset);
        Assert.Equal("component", UcliOperationReferenceTargetKindValues.Component);
        Assert.Equal("gameObject", UcliOperationReferenceTargetKindValues.GameObject);
        Assert.Equal("component", UcliOperationTypeKindValues.Component);
        Assert.Equal("write", UcliOperationSerializedPropertyAccessValues.Write);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceEnums_HaveStableValues ()
    {
        Assert.Equal(0, (int)UcliOperationSideEffect.OpensSceneInEditor);
        Assert.Equal(1, (int)UcliOperationSideEffect.OpensPrefabStage);
        Assert.Equal(2, (int)UcliOperationSideEffect.RefreshesAssetDatabase);
        Assert.Equal(3, (int)UcliOperationSideEffect.WritesAsset);
        Assert.Equal(4, (int)UcliOperationSideEffect.WritesScene);
        Assert.Equal(5, (int)UcliOperationSideEffect.WritesPrefab);
        Assert.Equal(6, (int)UcliOperationSideEffect.WritesProjectSettings);
        Assert.Equal(0, (int)UcliOperationPlanMode.ValidationOnly);
        Assert.Equal(1, (int)UcliOperationPlanMode.ObservesLiveUnity);
        Assert.Equal(2, (int)UcliOperationPlanMode.MayCreatePreviewState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceCodecs_ToValue_ReturnStableLiterals ()
    {
        Assert.Equal(UcliOperationSideEffectValues.OpensSceneInEditor, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.OpensSceneInEditor));
        Assert.Equal(UcliOperationSideEffectValues.OpensPrefabStage, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.OpensPrefabStage));
        Assert.Equal(UcliOperationSideEffectValues.RefreshesAssetDatabase, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.RefreshesAssetDatabase));
        Assert.Equal(UcliOperationSideEffectValues.WritesAsset, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.WritesAsset));
        Assert.Equal(UcliOperationSideEffectValues.WritesScene, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.WritesScene));
        Assert.Equal(UcliOperationSideEffectValues.WritesPrefab, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.WritesPrefab));
        Assert.Equal(UcliOperationSideEffectValues.WritesProjectSettings, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.WritesProjectSettings));
        Assert.Equal(UcliOperationPlanModeValues.ValidationOnly, UcliOperationPlanModeCodec.ToValue(UcliOperationPlanMode.ValidationOnly));
        Assert.Equal(UcliOperationPlanModeValues.ObservesLiveUnity, UcliOperationPlanModeCodec.ToValue(UcliOperationPlanMode.ObservesLiveUnity));
        Assert.Equal(UcliOperationPlanModeValues.MayCreatePreviewState, UcliOperationPlanModeCodec.ToValue(UcliOperationPlanMode.MayCreatePreviewState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceValues_AreStableLiterals ()
    {
        Assert.Equal("opensSceneInEditor", UcliOperationSideEffectValues.OpensSceneInEditor);
        Assert.Equal("opensPrefabStage", UcliOperationSideEffectValues.OpensPrefabStage);
        Assert.Equal("refreshesAssetDatabase", UcliOperationSideEffectValues.RefreshesAssetDatabase);
        Assert.Equal("writesAsset", UcliOperationSideEffectValues.WritesAsset);
        Assert.Equal("writesScene", UcliOperationSideEffectValues.WritesScene);
        Assert.Equal("writesPrefab", UcliOperationSideEffectValues.WritesPrefab);
        Assert.Equal("writesProjectSettings", UcliOperationSideEffectValues.WritesProjectSettings);
        Assert.Equal("validationOnly", UcliOperationPlanModeValues.ValidationOnly);
        Assert.Equal("observesLiveUnity", UcliOperationPlanModeValues.ObservesLiveUnity);
        Assert.Equal("mayCreatePreviewState", UcliOperationPlanModeValues.MayCreatePreviewState);
    }
}
