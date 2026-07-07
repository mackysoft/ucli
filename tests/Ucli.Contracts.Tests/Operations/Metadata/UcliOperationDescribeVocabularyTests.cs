using System.Reflection;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Operations;

public sealed class UcliOperationDescribeVocabularyTests
{
    private static readonly SideEffectMinimumPolicyCase[] SideEffectMinimumPolicyCases =
    [
        new("observesUnityState", OperationPolicy.Safe),
        new("editorStateChange", OperationPolicy.Advanced),
        new("opensSceneInEditor", OperationPolicy.Advanced),
        new("opensPrefabStage", OperationPolicy.Advanced),
        new("assetDatabaseRefresh", OperationPolicy.Advanced),
        new("assetImport", OperationPolicy.Advanced),
        new("scriptCompilation", OperationPolicy.Advanced),
        new("domainReload", OperationPolicy.Advanced),
        new("sceneContentMutation", OperationPolicy.Advanced),
        new("prefabContentMutation", OperationPolicy.Advanced),
        new("assetContentMutation", OperationPolicy.Advanced),
        new("projectSettingsMutation", OperationPolicy.Advanced),
        new("sceneSave", OperationPolicy.Advanced),
        new("prefabSave", OperationPolicy.Advanced),
        new("assetSave", OperationPolicy.Advanced),
        new("projectSave", OperationPolicy.Advanced),
        new("externalProcess", OperationPolicy.Dangerous),
        new("filesystemWrite", OperationPolicy.Dangerous),
        new("arbitrarySourceExecution", OperationPolicy.Dangerous),
        new("destructiveScope", OperationPolicy.Dangerous),
        new("runtimeStateMutation", OperationPolicy.Advanced),
    ];

    private static readonly SideEffectQueryAllowanceCase[] SideEffectQueryAllowanceCases =
    [
        new("observesUnityState", ExpectedAllowed: true),
        new("editorStateChange", ExpectedAllowed: false),
        new("opensSceneInEditor", ExpectedAllowed: false),
        new("opensPrefabStage", ExpectedAllowed: false),
        new("assetDatabaseRefresh", ExpectedAllowed: false),
        new("assetImport", ExpectedAllowed: false),
        new("scriptCompilation", ExpectedAllowed: false),
        new("domainReload", ExpectedAllowed: false),
        new("sceneContentMutation", ExpectedAllowed: false),
        new("prefabContentMutation", ExpectedAllowed: false),
        new("assetContentMutation", ExpectedAllowed: false),
        new("projectSettingsMutation", ExpectedAllowed: false),
        new("sceneSave", ExpectedAllowed: false),
        new("prefabSave", ExpectedAllowed: false),
        new("assetSave", ExpectedAllowed: false),
        new("projectSave", ExpectedAllowed: false),
        new("externalProcess", ExpectedAllowed: false),
        new("filesystemWrite", ExpectedAllowed: false),
        new("arbitrarySourceExecution", ExpectedAllowed: false),
        new("destructiveScope", ExpectedAllowed: false),
        new("runtimeStateMutation", ExpectedAllowed: false),
    ];

    private static readonly SideEffectAssuranceProjectionCase[] SideEffectAssuranceProjectionCases =
    [
        new("observesUnityState", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("editorStateChange", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("opensSceneInEditor", ExpectedMayDirty: false, ExpectedMayPersist: false, [UcliTouchedResourceKindNames.Scene]),
        new("opensPrefabStage", ExpectedMayDirty: false, ExpectedMayPersist: false, [UcliTouchedResourceKindNames.Prefab]),
        new("assetDatabaseRefresh", ExpectedMayDirty: false, ExpectedMayPersist: false, [UcliTouchedResourceKindNames.Asset]),
        new("assetImport", ExpectedMayDirty: false, ExpectedMayPersist: false, [UcliTouchedResourceKindNames.Asset]),
        new("scriptCompilation", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("domainReload", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("sceneContentMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, [UcliTouchedResourceKindNames.Scene]),
        new("prefabContentMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, [UcliTouchedResourceKindNames.Prefab]),
        new("assetContentMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, [UcliTouchedResourceKindNames.Asset]),
        new("projectSettingsMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, [UcliTouchedResourceKindNames.ProjectSettings]),
        new("sceneSave", ExpectedMayDirty: false, ExpectedMayPersist: true, [UcliTouchedResourceKindNames.Scene]),
        new("prefabSave", ExpectedMayDirty: false, ExpectedMayPersist: true, [UcliTouchedResourceKindNames.Prefab]),
        new("assetSave", ExpectedMayDirty: false, ExpectedMayPersist: true, [UcliTouchedResourceKindNames.Asset]),
        new(
            "projectSave",
            ExpectedMayDirty: false,
            ExpectedMayPersist: true,
            [
                UcliTouchedResourceKindNames.Scene,
                UcliTouchedResourceKindNames.Prefab,
                UcliTouchedResourceKindNames.Asset,
                UcliTouchedResourceKindNames.ProjectSettings,
            ]),
        new("externalProcess", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("filesystemWrite", ExpectedMayDirty: false, ExpectedMayPersist: true, []),
        new("arbitrarySourceExecution", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("destructiveScope", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("runtimeStateMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, []),
    ];

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
        Assert.Equal(20, (int)UcliOperationSideEffect.RuntimeStateMutation);
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
            .Select(testCase => testCase.SideEffect)
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, fixtureLiterals);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_DeclareMinimumPolicy ()
    {
        foreach (var testCase in SideEffectMinimumPolicyCases)
        {
            var isSupported = UcliOperationSideEffectDescriptors.TryGetMinimumPolicy(testCase.SideEffect, out var policy);

            Assert.True(isSupported);
            Assert.Equal(testCase.ExpectedPolicy, policy);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_DangerousDerivationMatchesMinimumPolicy ()
    {
        foreach (var testCase in SideEffectMinimumPolicyCases)
        {
            var isDangerousSource = UcliOperationSideEffectDescriptors.IsDangerousDerivationSource(testCase.SideEffect);

            Assert.Equal(testCase.ExpectedPolicy == OperationPolicy.Dangerous, isDangerousSource);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_QueryOperationAllowanceFixturesCoverSupportedValues ()
    {
        var fixtureLiterals = SideEffectQueryAllowanceCases
            .Select(testCase => testCase.SideEffect)
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, fixtureLiterals);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_DeclareQueryOperationAllowance ()
    {
        foreach (var testCase in SideEffectQueryAllowanceCases)
        {
            var isAllowed = UcliOperationSideEffectDescriptors.IsAllowedForQueryOperation(testCase.SideEffect);

            Assert.Equal(testCase.ExpectedAllowed, isAllowed);

            if (testCase.ExpectedAllowed)
            {
                Assert.True(UcliOperationSideEffectDescriptors.TryGetDescriptor(testCase.SideEffect, out var descriptor));
                Assert.Equal(OperationPolicy.Safe, descriptor!.MinimumPolicy);
                Assert.False(descriptor.DerivesMayDirty);
                Assert.False(descriptor.DerivesMayPersist);
                Assert.Empty(descriptor.RequiredTouchedKinds);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_AssuranceProjectionFixturesCoverSupportedValues ()
    {
        var fixtureLiterals = SideEffectAssuranceProjectionCases
            .Select(testCase => testCase.SideEffect)
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, fixtureLiterals);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_DeclareAssuranceProjectionAndTouchedKindConstraints ()
    {
        foreach (var testCase in SideEffectAssuranceProjectionCases)
        {
            var isSupported = UcliOperationSideEffectDescriptors.TryGetDescriptor(testCase.SideEffect, out var descriptor);

            Assert.True(isSupported);
            Assert.Equal(testCase.ExpectedMayDirty, descriptor!.DerivesMayDirty);
            Assert.Equal(testCase.ExpectedMayPersist, descriptor.DerivesMayPersist);
            Assert.Equal(testCase.ExpectedRequiredTouchedKinds, descriptor.RequiredTouchedKinds);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_DeriveAssuranceProjection ()
    {
        foreach (var testCase in SideEffectAssuranceProjectionCases)
        {
            var isSupported = UcliOperationSideEffectDescriptors.TryDeriveAssuranceProjection(
                [testCase.SideEffect],
                out var mayDirty,
                out var mayPersist);

            Assert.True(isSupported);
            Assert.Equal(testCase.ExpectedMayDirty, mayDirty);
            Assert.Equal(testCase.ExpectedMayPersist, mayPersist);
        }
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

    private sealed record SideEffectMinimumPolicyCase (
        string SideEffect,
        OperationPolicy ExpectedPolicy);

    private sealed record SideEffectQueryAllowanceCase (
        string SideEffect,
        bool ExpectedAllowed);

    private sealed record SideEffectAssuranceProjectionCase (
        string SideEffect,
        bool ExpectedMayDirty,
        bool ExpectedMayPersist,
        string[] ExpectedRequiredTouchedKinds);
}
