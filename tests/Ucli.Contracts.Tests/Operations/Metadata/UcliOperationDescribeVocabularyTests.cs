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
        new("opensSceneInEditor", ExpectedMayDirty: false, ExpectedMayPersist: false, [UcliTouchedResourceKind.Scene]),
        new("opensPrefabStage", ExpectedMayDirty: false, ExpectedMayPersist: false, [UcliTouchedResourceKind.Prefab]),
        new("assetDatabaseRefresh", ExpectedMayDirty: false, ExpectedMayPersist: false, [UcliTouchedResourceKind.Asset]),
        new("assetImport", ExpectedMayDirty: false, ExpectedMayPersist: false, [UcliTouchedResourceKind.Asset]),
        new("scriptCompilation", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("domainReload", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("sceneContentMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, [UcliTouchedResourceKind.Scene]),
        new("prefabContentMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, [UcliTouchedResourceKind.Prefab]),
        new("assetContentMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, [UcliTouchedResourceKind.Asset]),
        new("projectSettingsMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, [UcliTouchedResourceKind.ProjectSettings]),
        new("sceneSave", ExpectedMayDirty: false, ExpectedMayPersist: true, [UcliTouchedResourceKind.Scene]),
        new("prefabSave", ExpectedMayDirty: false, ExpectedMayPersist: true, [UcliTouchedResourceKind.Prefab]),
        new("assetSave", ExpectedMayDirty: false, ExpectedMayPersist: true, [UcliTouchedResourceKind.Asset]),
        new(
            "projectSave",
            ExpectedMayDirty: false,
            ExpectedMayPersist: true,
            [
                UcliTouchedResourceKind.Scene,
                UcliTouchedResourceKind.Prefab,
                UcliTouchedResourceKind.Asset,
                UcliTouchedResourceKind.ProjectSettings,
            ]),
        new("externalProcess", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("filesystemWrite", ExpectedMayDirty: false, ExpectedMayPersist: true, []),
        new("arbitrarySourceExecution", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("destructiveScope", ExpectedMayDirty: false, ExpectedMayPersist: false, []),
        new("runtimeStateMutation", ExpectedMayDirty: true, ExpectedMayPersist: false, []),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_CoverAllSideEffectLiterals ()
    {
        var enumLiterals = Enum
            .GetValues<UcliOperationSideEffect>()
            .Select(TextVocabulary.GetText)
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
            .Select(descriptor => TextVocabulary.GetText(descriptor.SideEffect))
            .ToArray();

        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, descriptorLiterals);
        Assert.Equal(UcliOperationSideEffectDescriptors.SupportedValues, descriptorCodecLiterals);
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
            var isSupported = TextVocabulary.TryGetValue(
                testCase.SideEffect,
                out UcliOperationSideEffect sideEffect);

            Assert.True(isSupported);
            Assert.Equal(
                testCase.ExpectedPolicy,
                UcliOperationSideEffectDescriptors.GetDescriptor(sideEffect).MinimumPolicy);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SideEffectDescriptors_DangerousDerivationMatchesMinimumPolicy ()
    {
        foreach (var testCase in SideEffectMinimumPolicyCases)
        {
            Assert.True(TextVocabulary.TryGetValue(
                testCase.SideEffect,
                out UcliOperationSideEffect sideEffect));
            var isDangerousSource = UcliOperationSideEffectDescriptors.GetDescriptor(sideEffect).MinimumPolicy
                == OperationPolicy.Dangerous;

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
            Assert.True(TextVocabulary.TryGetValue(
                testCase.SideEffect,
                out UcliOperationSideEffect sideEffect));
            var descriptor = UcliOperationSideEffectDescriptors.GetDescriptor(sideEffect);
            var isAllowed = descriptor.AllowedForQueryOperation;

            Assert.Equal(testCase.ExpectedAllowed, isAllowed);

            if (testCase.ExpectedAllowed)
            {
                Assert.Equal(OperationPolicy.Safe, descriptor.MinimumPolicy);
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
            var isSupported = TextVocabulary.TryGetValue(
                testCase.SideEffect,
                out UcliOperationSideEffect sideEffect);

            Assert.True(isSupported);
            var descriptor = UcliOperationSideEffectDescriptors.GetDescriptor(sideEffect);
            Assert.Equal(testCase.ExpectedMayDirty, descriptor.DerivesMayDirty);
            Assert.Equal(testCase.ExpectedMayPersist, descriptor.DerivesMayPersist);
            Assert.Equal(testCase.ExpectedRequiredTouchedKinds, descriptor.RequiredTouchedKinds);
        }
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
        UcliTouchedResourceKind[] ExpectedRequiredTouchedKinds);
}
