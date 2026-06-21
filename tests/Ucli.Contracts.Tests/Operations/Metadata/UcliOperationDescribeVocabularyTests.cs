using System.Reflection;
using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Operations;

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
    public void SideEffectDescriptors_CoverAllSideEffectLiterals ()
    {
        var enumLiterals = Enum
            .GetValues<UcliOperationSideEffect>()
            .Select(ContractLiteralCodec.ToValue)
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
            .Select(descriptor => ContractLiteralCodec.ToValue(descriptor.SideEffect))
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
        Assert.DoesNotContain("MackySoft.Ucli.Contracts.Operations.UcliOperationSideEffectDescriptor", exportedTypeNames);

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
    public void SideEffectDescriptors_QueryOperationAllowanceFixturesCoverSupportedValues ()
    {
        var fixtureLiterals = SideEffectQueryAllowanceCases
            .Select(values => Assert.IsType<string>(values[0]))
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, fixtureLiterals);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SideEffectQueryAllowanceCases))]
    public void SideEffectDescriptors_DeclareQueryOperationAllowance (
        string sideEffect,
        bool expectedAllowed)
    {
        var isAllowed = UcliOperationSideEffectDescriptors.IsAllowedForQueryOperation(sideEffect);

        Assert.Equal(expectedAllowed, isAllowed);

        if (expectedAllowed)
        {
            Assert.True(UcliOperationSideEffectDescriptors.TryGetDescriptor(sideEffect, out var descriptor));
            Assert.Equal(OperationPolicy.Safe, descriptor!.MinimumPolicy);
            Assert.False(descriptor.DerivesMayDirty);
            Assert.False(descriptor.DerivesMayPersist);
            Assert.Empty(descriptor.RequiredTouchedKinds);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_AssuranceProjectionFixturesCoverSupportedValues ()
    {
        var fixtureLiterals = SideEffectAssuranceProjectionCases
            .Select(values => Assert.IsType<string>(values[0]))
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, fixtureLiterals);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SideEffectAssuranceProjectionCases))]
    public void SideEffectDescriptors_DeclareAssuranceProjectionAndTouchedKindConstraints (
        string sideEffect,
        bool expectedMayDirty,
        bool expectedMayPersist,
        string[] expectedRequiredTouchedKinds)
    {
        var isSupported = UcliOperationSideEffectDescriptors.TryGetDescriptor(sideEffect, out var descriptor);

        Assert.True(isSupported);
        Assert.Equal(expectedMayDirty, descriptor!.DerivesMayDirty);
        Assert.Equal(expectedMayPersist, descriptor.DerivesMayPersist);
        Assert.Equal(expectedRequiredTouchedKinds, descriptor.RequiredTouchedKinds);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(SideEffectAssuranceProjectionCases))]
    public void SideEffectDescriptors_DeriveAssuranceProjection (
        string sideEffect,
        bool expectedMayDirty,
        bool expectedMayPersist,
        string[] _)
    {
        var isSupported = UcliOperationSideEffectDescriptors.TryDeriveAssuranceProjection([sideEffect], out var mayDirty, out var mayPersist);

        Assert.True(isSupported);
        Assert.Equal(expectedMayDirty, mayDirty);
        Assert.Equal(expectedMayPersist, mayPersist);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_TryDeriveAssuranceProjection_WhenSideEffectIsUnsupported_ReturnsFalse ()
    {
        var isSupported = UcliOperationSideEffectDescriptors.TryDeriveAssuranceProjection(["not-supported"], out var mayDirty, out var mayPersist);

        Assert.False(isSupported);
        Assert.False(mayDirty);
        Assert.False(mayPersist);
    }

    public static IEnumerable<object[]> SideEffectMinimumPolicyCases
    {
        get
        {
            yield return new object[] { "observesUnityState", OperationPolicy.Safe };
            yield return new object[] { "editorStateChange", OperationPolicy.Advanced };
            yield return new object[] { "opensSceneInEditor", OperationPolicy.Advanced };
            yield return new object[] { "opensPrefabStage", OperationPolicy.Advanced };
            yield return new object[] { "assetDatabaseRefresh", OperationPolicy.Advanced };
            yield return new object[] { "assetImport", OperationPolicy.Advanced };
            yield return new object[] { "scriptCompilation", OperationPolicy.Advanced };
            yield return new object[] { "domainReload", OperationPolicy.Advanced };
            yield return new object[] { "sceneContentMutation", OperationPolicy.Advanced };
            yield return new object[] { "prefabContentMutation", OperationPolicy.Advanced };
            yield return new object[] { "assetContentMutation", OperationPolicy.Advanced };
            yield return new object[] { "projectSettingsMutation", OperationPolicy.Advanced };
            yield return new object[] { "sceneSave", OperationPolicy.Advanced };
            yield return new object[] { "prefabSave", OperationPolicy.Advanced };
            yield return new object[] { "assetSave", OperationPolicy.Advanced };
            yield return new object[] { "projectSave", OperationPolicy.Advanced };
            yield return new object[] { "externalProcess", OperationPolicy.Dangerous };
            yield return new object[] { "filesystemWrite", OperationPolicy.Dangerous };
            yield return new object[] { "arbitrarySourceExecution", OperationPolicy.Dangerous };
            yield return new object[] { "destructiveScope", OperationPolicy.Dangerous };
        }
    }

    public static IEnumerable<object[]> SideEffectQueryAllowanceCases
    {
        get
        {
            yield return new object[] { "observesUnityState", true };
            yield return new object[] { "editorStateChange", false };
            yield return new object[] { "opensSceneInEditor", false };
            yield return new object[] { "opensPrefabStage", false };
            yield return new object[] { "assetDatabaseRefresh", false };
            yield return new object[] { "assetImport", false };
            yield return new object[] { "scriptCompilation", false };
            yield return new object[] { "domainReload", false };
            yield return new object[] { "sceneContentMutation", false };
            yield return new object[] { "prefabContentMutation", false };
            yield return new object[] { "assetContentMutation", false };
            yield return new object[] { "projectSettingsMutation", false };
            yield return new object[] { "sceneSave", false };
            yield return new object[] { "prefabSave", false };
            yield return new object[] { "assetSave", false };
            yield return new object[] { "projectSave", false };
            yield return new object[] { "externalProcess", false };
            yield return new object[] { "filesystemWrite", false };
            yield return new object[] { "arbitrarySourceExecution", false };
            yield return new object[] { "destructiveScope", false };
        }
    }

    public static IEnumerable<object[]> SideEffectAssuranceProjectionCases
    {
        get
        {
            yield return new object[] { "observesUnityState", false, false, Array.Empty<string>() };
            yield return new object[] { "editorStateChange", false, false, Array.Empty<string>() };
            yield return new object[] { "opensSceneInEditor", false, false, new[] { UcliTouchedResourceKindNames.Scene } };
            yield return new object[] { "opensPrefabStage", false, false, new[] { UcliTouchedResourceKindNames.Prefab } };
            yield return new object[] { "assetDatabaseRefresh", false, false, new[] { UcliTouchedResourceKindNames.Asset } };
            yield return new object[] { "assetImport", false, false, new[] { UcliTouchedResourceKindNames.Asset } };
            yield return new object[] { "scriptCompilation", false, false, Array.Empty<string>() };
            yield return new object[] { "domainReload", false, false, Array.Empty<string>() };
            yield return new object[] { "sceneContentMutation", true, false, new[] { UcliTouchedResourceKindNames.Scene } };
            yield return new object[] { "prefabContentMutation", true, false, new[] { UcliTouchedResourceKindNames.Prefab } };
            yield return new object[] { "assetContentMutation", true, false, new[] { UcliTouchedResourceKindNames.Asset } };
            yield return new object[] { "projectSettingsMutation", true, false, new[] { UcliTouchedResourceKindNames.ProjectSettings } };
            yield return new object[] { "sceneSave", false, true, new[] { UcliTouchedResourceKindNames.Scene } };
            yield return new object[] { "prefabSave", false, true, new[] { UcliTouchedResourceKindNames.Prefab } };
            yield return new object[] { "assetSave", false, true, new[] { UcliTouchedResourceKindNames.Asset } };
            yield return new object[]
            {
                "projectSave",
                false,
                true,
                new[]
                {
                    UcliTouchedResourceKindNames.Scene,
                    UcliTouchedResourceKindNames.Prefab,
                    UcliTouchedResourceKindNames.Asset,
                    UcliTouchedResourceKindNames.ProjectSettings,
                },
            };
            yield return new object[] { "externalProcess", false, false, Array.Empty<string>() };
            yield return new object[] { "filesystemWrite", false, true, Array.Empty<string>() };
            yield return new object[] { "arbitrarySourceExecution", false, false, Array.Empty<string>() };
            yield return new object[] { "destructiveScope", false, false, Array.Empty<string>() };
        }
    }
}
