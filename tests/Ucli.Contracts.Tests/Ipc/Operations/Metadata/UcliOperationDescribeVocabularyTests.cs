using System.Reflection;
using MackySoft.Ucli.Contracts.Configuration;
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
        Assert.Equal(0, (int)UcliOperationSideEffect.ObservesUnityState);
        Assert.Equal(1, (int)UcliOperationSideEffect.EditorStateChange);
        Assert.Equal(2, (int)UcliOperationSideEffect.OpensSceneInEditor);
        Assert.Equal(3, (int)UcliOperationSideEffect.OpensPrefabStage);
        Assert.Equal(4, (int)UcliOperationSideEffect.AssetDatabaseRefresh);
        Assert.Equal(5, (int)UcliOperationSideEffect.AssetImport);
        Assert.Equal(6, (int)UcliOperationSideEffect.ScriptCompilation);
        Assert.Equal(7, (int)UcliOperationSideEffect.DomainReload);
        Assert.Equal(8, (int)UcliOperationSideEffect.SceneContentMutation);
        Assert.Equal(9, (int)UcliOperationSideEffect.PrefabContentMutation);
        Assert.Equal(10, (int)UcliOperationSideEffect.AssetContentMutation);
        Assert.Equal(11, (int)UcliOperationSideEffect.ProjectSettingsMutation);
        Assert.Equal(12, (int)UcliOperationSideEffect.SceneSave);
        Assert.Equal(13, (int)UcliOperationSideEffect.PrefabSave);
        Assert.Equal(14, (int)UcliOperationSideEffect.AssetSave);
        Assert.Equal(15, (int)UcliOperationSideEffect.ProjectSave);
        Assert.Equal(16, (int)UcliOperationSideEffect.ExternalProcess);
        Assert.Equal(17, (int)UcliOperationSideEffect.FilesystemWrite);
        Assert.Equal(18, (int)UcliOperationSideEffect.ArbitrarySourceExecution);
        Assert.Equal(19, (int)UcliOperationSideEffect.DestructiveScope);
        Assert.Equal(0, (int)UcliOperationPlanMode.ValidationOnly);
        Assert.Equal(1, (int)UcliOperationPlanMode.ObservesLiveUnity);
        Assert.Equal(2, (int)UcliOperationPlanMode.MayCreatePreviewState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceCodecs_ToValue_ReturnStableLiterals ()
    {
        Assert.Equal(UcliOperationSideEffectValues.ObservesUnityState, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.ObservesUnityState));
        Assert.Equal(UcliOperationSideEffectValues.EditorStateChange, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.EditorStateChange));
        Assert.Equal(UcliOperationSideEffectValues.OpensSceneInEditor, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.OpensSceneInEditor));
        Assert.Equal(UcliOperationSideEffectValues.OpensPrefabStage, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.OpensPrefabStage));
        Assert.Equal(UcliOperationSideEffectValues.AssetDatabaseRefresh, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.AssetDatabaseRefresh));
        Assert.Equal(UcliOperationSideEffectValues.AssetImport, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.AssetImport));
        Assert.Equal(UcliOperationSideEffectValues.ScriptCompilation, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.ScriptCompilation));
        Assert.Equal(UcliOperationSideEffectValues.DomainReload, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.DomainReload));
        Assert.Equal(UcliOperationSideEffectValues.SceneContentMutation, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.SceneContentMutation));
        Assert.Equal(UcliOperationSideEffectValues.PrefabContentMutation, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.PrefabContentMutation));
        Assert.Equal(UcliOperationSideEffectValues.AssetContentMutation, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.AssetContentMutation));
        Assert.Equal(UcliOperationSideEffectValues.ProjectSettingsMutation, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.ProjectSettingsMutation));
        Assert.Equal(UcliOperationSideEffectValues.SceneSave, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.SceneSave));
        Assert.Equal(UcliOperationSideEffectValues.PrefabSave, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.PrefabSave));
        Assert.Equal(UcliOperationSideEffectValues.AssetSave, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.AssetSave));
        Assert.Equal(UcliOperationSideEffectValues.ProjectSave, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.ProjectSave));
        Assert.Equal(UcliOperationSideEffectValues.ExternalProcess, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.ExternalProcess));
        Assert.Equal(UcliOperationSideEffectValues.FilesystemWrite, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.FilesystemWrite));
        Assert.Equal(UcliOperationSideEffectValues.ArbitrarySourceExecution, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.ArbitrarySourceExecution));
        Assert.Equal(UcliOperationSideEffectValues.DestructiveScope, UcliOperationSideEffectCodec.ToValue(UcliOperationSideEffect.DestructiveScope));
        Assert.Equal(UcliOperationPlanModeValues.ValidationOnly, UcliOperationPlanModeCodec.ToValue(UcliOperationPlanMode.ValidationOnly));
        Assert.Equal(UcliOperationPlanModeValues.ObservesLiveUnity, UcliOperationPlanModeCodec.ToValue(UcliOperationPlanMode.ObservesLiveUnity));
        Assert.Equal(UcliOperationPlanModeValues.MayCreatePreviewState, UcliOperationPlanModeCodec.ToValue(UcliOperationPlanMode.MayCreatePreviewState));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceValues_AreStableLiterals ()
    {
        Assert.Equal("observesUnityState", UcliOperationSideEffectValues.ObservesUnityState);
        Assert.Equal("editorStateChange", UcliOperationSideEffectValues.EditorStateChange);
        Assert.Equal("opensSceneInEditor", UcliOperationSideEffectValues.OpensSceneInEditor);
        Assert.Equal("opensPrefabStage", UcliOperationSideEffectValues.OpensPrefabStage);
        Assert.Equal("assetDatabaseRefresh", UcliOperationSideEffectValues.AssetDatabaseRefresh);
        Assert.Equal("assetImport", UcliOperationSideEffectValues.AssetImport);
        Assert.Equal("scriptCompilation", UcliOperationSideEffectValues.ScriptCompilation);
        Assert.Equal("domainReload", UcliOperationSideEffectValues.DomainReload);
        Assert.Equal("sceneContentMutation", UcliOperationSideEffectValues.SceneContentMutation);
        Assert.Equal("prefabContentMutation", UcliOperationSideEffectValues.PrefabContentMutation);
        Assert.Equal("assetContentMutation", UcliOperationSideEffectValues.AssetContentMutation);
        Assert.Equal("projectSettingsMutation", UcliOperationSideEffectValues.ProjectSettingsMutation);
        Assert.Equal("sceneSave", UcliOperationSideEffectValues.SceneSave);
        Assert.Equal("prefabSave", UcliOperationSideEffectValues.PrefabSave);
        Assert.Equal("assetSave", UcliOperationSideEffectValues.AssetSave);
        Assert.Equal("projectSave", UcliOperationSideEffectValues.ProjectSave);
        Assert.Equal("externalProcess", UcliOperationSideEffectValues.ExternalProcess);
        Assert.Equal("filesystemWrite", UcliOperationSideEffectValues.FilesystemWrite);
        Assert.Equal("arbitrarySourceExecution", UcliOperationSideEffectValues.ArbitrarySourceExecution);
        Assert.Equal("destructiveScope", UcliOperationSideEffectValues.DestructiveScope);
        Assert.Equal("validationOnly", UcliOperationPlanModeValues.ValidationOnly);
        Assert.Equal("observesLiveUnity", UcliOperationPlanModeValues.ObservesLiveUnity);
        Assert.Equal("mayCreatePreviewState", UcliOperationPlanModeValues.MayCreatePreviewState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_CoverAllSideEffectLiterals ()
    {
        var enumLiterals = Enum
            .GetValues<UcliOperationSideEffect>()
            .Select(UcliOperationSideEffectCodec.ToValue)
            .ToArray();

        Assert.Equal(enumLiterals, UcliOperationSideEffectDescriptors.SupportedValues);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_ExposeDescriptorsForSupportedValues ()
    {
        var descriptorLiterals = UcliOperationSideEffectDescriptors.All
            .Select(descriptor => descriptor.Value)
            .ToArray();
        var descriptorCodecLiterals = UcliOperationSideEffectDescriptors.All
            .Select(descriptor => UcliOperationSideEffectCodec.ToValue(descriptor.SideEffect))
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, descriptorLiterals);
        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, descriptorCodecLiterals);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptorImplementationSurface_IsNotPublicApi ()
    {
        var exportedTypeNames = typeof(UcliOperationSideEffect).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName)
            .ToArray();

        Assert.DoesNotContain("MackySoft.Ucli.Contracts.Ipc.UcliOperationPolicyDeriver", exportedTypeNames);
        Assert.DoesNotContain("MackySoft.Ucli.Contracts.Ipc.UcliOperationSideEffectDescriptor", exportedTypeNames);
        Assert.DoesNotContain("MackySoft.Ucli.Contracts.Ipc.UcliOperationSideEffectRequiredAssuranceFact", exportedTypeNames);
        Assert.DoesNotContain("MackySoft.Ucli.Contracts.Ipc.UcliOperationSideEffectRequiredAssuranceFactKind", exportedTypeNames);

        var publicStaticProperties = typeof(UcliOperationSideEffectDescriptors)
            .GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToArray();
        var publicStaticMethods = typeof(UcliOperationSideEffectDescriptors)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        Assert.Equal(new[] { nameof(UcliOperationSideEffectDescriptors.SupportedValues) }, publicStaticProperties);
        Assert.Empty(publicStaticMethods);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_MinimumPolicyFixturesCoverSupportedValues ()
    {
        var fixtureLiterals = SideEffectMinimumPolicyCases
            .Select(values => Assert.IsType<string>(values[0]))
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, fixtureLiterals);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SideEffectMinimumPolicyCases))]
    public void SideEffectDescriptors_DeclareMinimumPolicy (
        string sideEffect,
        OperationPolicy expectedPolicy)
    {
        var isSupported = UcliOperationSideEffectDescriptors.TryGetMinimumPolicy(sideEffect, out var policy);

        Assert.True(isSupported);
        Assert.Equal(expectedPolicy, policy);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SideEffectMinimumPolicyCases))]
    public void SideEffectDescriptors_DangerousDerivationMatchesMinimumPolicy (
        string sideEffect,
        OperationPolicy expectedPolicy)
    {
        var isDangerousSource = UcliOperationSideEffectDescriptors.IsDangerousDerivationSource(sideEffect);

        Assert.Equal(expectedPolicy == OperationPolicy.Dangerous, isDangerousSource);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_QueryAllowanceFixturesCoverSupportedValues ()
    {
        var fixtureLiterals = SideEffectQueryAllowanceCases
            .Select(values => Assert.IsType<string>(values[0]))
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, fixtureLiterals);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SideEffectQueryAllowanceCases))]
    public void SideEffectDescriptors_DeclareQueryAllowance (
        string sideEffect,
        bool expectedAllowed)
    {
        var isAllowed = UcliOperationSideEffectDescriptors.IsAllowedForQuery(sideEffect);

        Assert.Equal(expectedAllowed, isAllowed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_RequiredAssuranceFactFixturesCoverSupportedValues ()
    {
        var fixtureLiterals = SideEffectRequiredAssuranceFactCases
            .Select(values => Assert.IsType<string>(values[0]))
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, fixtureLiterals);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SideEffectRequiredAssuranceFactCases))]
    public void SideEffectDescriptors_DeclareRequiredAssuranceFacts (
        string sideEffect,
        string[] expectedFacts)
    {
        var isSupported = UcliOperationSideEffectDescriptors.TryGetDescriptor(sideEffect, out var descriptor);

        Assert.True(isSupported);

        var actualFacts = descriptor!.RequiredAssuranceFacts
            .Select(FormatRequiredAssuranceFact)
            .ToArray();

        Assert.Equal(expectedFacts, actualFacts);
    }

    public static IEnumerable<object[]> SideEffectMinimumPolicyCases
    {
        get
        {
            yield return new object[] { UcliOperationSideEffectValues.ObservesUnityState, OperationPolicy.Safe };
            yield return new object[] { UcliOperationSideEffectValues.EditorStateChange, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.OpensSceneInEditor, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.OpensPrefabStage, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.AssetDatabaseRefresh, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.AssetImport, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.ScriptCompilation, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.DomainReload, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.SceneContentMutation, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.PrefabContentMutation, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.AssetContentMutation, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.ProjectSettingsMutation, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.SceneSave, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.PrefabSave, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.AssetSave, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.ProjectSave, OperationPolicy.Advanced };
            yield return new object[] { UcliOperationSideEffectValues.ExternalProcess, OperationPolicy.Dangerous };
            yield return new object[] { UcliOperationSideEffectValues.FilesystemWrite, OperationPolicy.Dangerous };
            yield return new object[] { UcliOperationSideEffectValues.ArbitrarySourceExecution, OperationPolicy.Dangerous };
            yield return new object[] { UcliOperationSideEffectValues.DestructiveScope, OperationPolicy.Dangerous };
        }
    }

    public static IEnumerable<object[]> SideEffectQueryAllowanceCases
    {
        get
        {
            yield return new object[] { UcliOperationSideEffectValues.ObservesUnityState, true };
            yield return new object[] { UcliOperationSideEffectValues.EditorStateChange, false };
            yield return new object[] { UcliOperationSideEffectValues.OpensSceneInEditor, false };
            yield return new object[] { UcliOperationSideEffectValues.OpensPrefabStage, false };
            yield return new object[] { UcliOperationSideEffectValues.AssetDatabaseRefresh, false };
            yield return new object[] { UcliOperationSideEffectValues.AssetImport, false };
            yield return new object[] { UcliOperationSideEffectValues.ScriptCompilation, false };
            yield return new object[] { UcliOperationSideEffectValues.DomainReload, false };
            yield return new object[] { UcliOperationSideEffectValues.SceneContentMutation, false };
            yield return new object[] { UcliOperationSideEffectValues.PrefabContentMutation, false };
            yield return new object[] { UcliOperationSideEffectValues.AssetContentMutation, false };
            yield return new object[] { UcliOperationSideEffectValues.ProjectSettingsMutation, false };
            yield return new object[] { UcliOperationSideEffectValues.SceneSave, false };
            yield return new object[] { UcliOperationSideEffectValues.PrefabSave, false };
            yield return new object[] { UcliOperationSideEffectValues.AssetSave, false };
            yield return new object[] { UcliOperationSideEffectValues.ProjectSave, false };
            yield return new object[] { UcliOperationSideEffectValues.ExternalProcess, false };
            yield return new object[] { UcliOperationSideEffectValues.FilesystemWrite, false };
            yield return new object[] { UcliOperationSideEffectValues.ArbitrarySourceExecution, false };
            yield return new object[] { UcliOperationSideEffectValues.DestructiveScope, false };
        }
    }

    public static IEnumerable<object[]> SideEffectRequiredAssuranceFactCases
    {
        get
        {
            yield return new object[] { UcliOperationSideEffectValues.ObservesUnityState, Array.Empty<string>() };
            yield return new object[] { UcliOperationSideEffectValues.EditorStateChange, Array.Empty<string>() };
            yield return new object[] { UcliOperationSideEffectValues.OpensSceneInEditor, new[] { "touchedKinds:scene" } };
            yield return new object[] { UcliOperationSideEffectValues.OpensPrefabStage, new[] { "touchedKinds:prefab" } };
            yield return new object[] { UcliOperationSideEffectValues.AssetDatabaseRefresh, new[] { "touchedKinds:asset" } };
            yield return new object[] { UcliOperationSideEffectValues.AssetImport, new[] { "touchedKinds:asset" } };
            yield return new object[] { UcliOperationSideEffectValues.ScriptCompilation, Array.Empty<string>() };
            yield return new object[] { UcliOperationSideEffectValues.DomainReload, Array.Empty<string>() };
            yield return new object[] { UcliOperationSideEffectValues.SceneContentMutation, new[] { "mayDirty=true", "touchedKinds:scene" } };
            yield return new object[] { UcliOperationSideEffectValues.PrefabContentMutation, new[] { "mayDirty=true", "touchedKinds:prefab" } };
            yield return new object[] { UcliOperationSideEffectValues.AssetContentMutation, new[] { "mayDirty=true", "touchedKinds:asset" } };
            yield return new object[] { UcliOperationSideEffectValues.ProjectSettingsMutation, new[] { "mayDirty=true", "touchedKinds:projectSettings" } };
            yield return new object[] { UcliOperationSideEffectValues.SceneSave, new[] { "mayPersist=true", "touchedKinds:scene" } };
            yield return new object[] { UcliOperationSideEffectValues.PrefabSave, new[] { "mayPersist=true", "touchedKinds:prefab" } };
            yield return new object[] { UcliOperationSideEffectValues.AssetSave, new[] { "mayPersist=true", "touchedKinds:asset" } };
            yield return new object[]
            {
                UcliOperationSideEffectValues.ProjectSave,
                new[]
                {
                    "mayPersist=true",
                    "touchedKinds:scene",
                    "touchedKinds:prefab",
                    "touchedKinds:asset",
                    "touchedKinds:projectSettings",
                },
            };
            yield return new object[] { UcliOperationSideEffectValues.ExternalProcess, Array.Empty<string>() };
            yield return new object[] { UcliOperationSideEffectValues.FilesystemWrite, new[] { "mayPersist=true" } };
            yield return new object[] { UcliOperationSideEffectValues.ArbitrarySourceExecution, Array.Empty<string>() };
            yield return new object[] { UcliOperationSideEffectValues.DestructiveScope, Array.Empty<string>() };
        }
    }

    private static string FormatRequiredAssuranceFact (UcliOperationSideEffectRequiredAssuranceFact fact)
    {
        switch (fact.Kind)
        {
            case UcliOperationSideEffectRequiredAssuranceFactKind.MayDirtyTrue:
                return "mayDirty=true";

            case UcliOperationSideEffectRequiredAssuranceFactKind.MayPersistTrue:
                return "mayPersist=true";

            case UcliOperationSideEffectRequiredAssuranceFactKind.TouchedKindIncludes:
                return $"touchedKinds:{fact.Value}";

            default:
                throw new ArgumentOutOfRangeException(nameof(fact), fact.Kind, "Unsupported required assurance fact kind.");
        }
    }
}
