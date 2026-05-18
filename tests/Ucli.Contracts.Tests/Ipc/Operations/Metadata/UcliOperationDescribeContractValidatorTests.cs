using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationDescribeContractValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenMultiFieldVariantIsValid_ReturnsTrue ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                variants: new[]
                {
                    new UcliOperationInputVariantContract(
                        "bySceneHierarchyPath",
                        "Use scene path and hierarchy path.",
                        new[]
                        {
                            new UcliOperationInputVariantFieldContract(
                                "scene",
                                "$.target.scene",
                                "Scene asset path.",
                                new[]
                                {
                                    new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.AssetExists)
                                    {
                                        AssetKind = UcliOperationAssetKindValues.Scene,
                                    },
                                }),
                            new UcliOperationInputVariantFieldContract(
                                "hierarchyPath",
                                "$.target.hierarchyPath",
                                "Hierarchy path.",
                                new[]
                                {
                                    new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.HierarchyPath),
                                }),
                        }),
                }),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenInputArgsPathUsesRequestLocalAlias_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                argsPath: "$.target.var"),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract input 'target' must not expose request-local alias args path '$.target.var'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenImplicitInputArgsPathUsesInvalidName_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target['name']",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>()),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid input at index 0.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenImplicitInputArgsPathUsesRequestLocalAliasName_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "var",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>()),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid input at index 0.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenVariantFieldArgsPathUsesRequestLocalAlias_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                variants: new[]
                {
                    new UcliOperationInputVariantContract(
                        "byAlias",
                        "Use request-local alias.",
                        new[]
                        {
                            new UcliOperationInputVariantFieldContract(
                                "globalObjectId",
                                "$.target.var.globalObjectId",
                                "Resolved Unity GlobalObjectId.",
                                Array.Empty<UcliOperationInputConstraintContract>()),
                        }),
                }),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid input variant field at index 0.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenVariantFieldIsNull_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                variants: new[]
                {
                    new UcliOperationInputVariantContract(
                        "byAlias",
                        "Use request-local alias.",
                        new UcliOperationInputVariantFieldContract[]
                        {
                            null!,
                        }),
                }),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract variant 'byAlias' field at index 0 must not be null.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenVariantFieldsAreMissing_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                variants: new[]
                {
                    new UcliOperationInputVariantContract(
                        "byAlias",
                        "Use request-local alias.",
                        fields: null),
                }),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid input variant at index 0.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenInputArgsPathUsesUnsupportedSyntax_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                argsPath: "$.target.items[0]"),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract input 'target' has an invalid argsPath.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenInputArgsPathExceedsLengthLimit_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                argsPath: "$." + new string('a', 255)),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract input 'target' has an invalid argsPath.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpInputs_WhenVariantFieldNameDiffersFromArgsPathLeaf_ReturnsFalse ()
    {
        var inputs = new[]
        {
            new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                variants: new[]
                {
                    new UcliOperationInputVariantContract(
                        "byGlobalObjectId",
                        "Use global object id.",
                        new[]
                        {
                            new UcliOperationInputVariantFieldContract(
                                "id",
                                "$.target.globalObjectId",
                                "Resolved Unity GlobalObjectId.",
                                Array.Empty<UcliOperationInputConstraintContract>()),
                        }),
                }),
        };

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid input variant field at index 0.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractIsValid_ReturnsTrue ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [UcliOperationSideEffectValues.ArbitrarySourceExecution];
        describe.Assurance.DangerousNotes = ["Arbitrary source execution requires dangerous policy."];
        describe.CodeContract = CreateValidCodeContract();

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(nameof(UcliOperationAssuranceContract.PlanSemantics))]
    [InlineData(nameof(UcliOperationAssuranceContract.CallSemantics))]
    [InlineData(nameof(UcliOperationAssuranceContract.TouchedContract))]
    [InlineData(nameof(UcliOperationAssuranceContract.ReadPostconditionContract))]
    [InlineData(nameof(UcliOperationAssuranceContract.FailureSemantics))]
    public void TryValidatePublicRawOpDescribeContract_WhenAssuranceSemanticFieldIsMissing_ReturnsFalse (
        string fieldName)
    {
        var describe = CreateValidDescribeContract();
        switch (fieldName)
        {
            case nameof(UcliOperationAssuranceContract.PlanSemantics):
                describe.Assurance!.PlanSemantics = null;
                break;
            case nameof(UcliOperationAssuranceContract.CallSemantics):
                describe.Assurance!.CallSemantics = string.Empty;
                break;
            case nameof(UcliOperationAssuranceContract.TouchedContract):
                describe.Assurance!.TouchedContract = " ";
                break;
            case nameof(UcliOperationAssuranceContract.ReadPostconditionContract):
                describe.Assurance!.ReadPostconditionContract = null;
                break;
            case nameof(UcliOperationAssuranceContract.FailureSemantics):
                describe.Assurance!.FailureSemantics = string.Empty;
                break;
        }

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has invalid assurance metadata.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssuranceSideEffectIsUnsupported_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = ["not-supported"];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an unsupported side effect 'not-supported'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssuranceTouchedKindIsUnsupported_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.TouchedKinds = ["not-supported"];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an unsupported touched kind 'not-supported'.", errorMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(RequiredAssuranceFactFailureCases))]
    public void TryValidatePublicRawOpDescribeContract_WhenSideEffectRequiredAssuranceFactIsMissing_ReturnsFalse (
        string sideEffect,
        bool mayDirty,
        bool mayPersist,
        string[] touchedKinds,
        string expectedErrorMessage)
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [sideEffect];
        describe.Assurance.MayDirty = mayDirty;
        describe.Assurance.MayPersist = mayPersist;
        describe.Assurance.TouchedKinds = touchedKinds;

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal(expectedErrorMessage, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssurancePlanModeIsUnsupported_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.PlanMode = "not-supported";

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has invalid assurance metadata.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenDangerousNotesIsNull_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.DangerousNotes = null;

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has invalid assurance metadata.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenQueryObservesUnityState_ReturnsTrue ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [UcliOperationSideEffectValues.ObservesUnityState];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            describe,
            operationKind: "query",
            operationPolicy: "safe",
            ownerName: "Test contract",
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Empty(errorMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("mayDirty")]
    [InlineData("mayPersist")]
    [InlineData("sideEffects")]
    public void TryValidatePublicRawOpDescribeContract_WhenQueryAssuranceHasMutationRisk_ReturnsFalse (
        string mutationRisk)
    {
        var describe = CreateValidDescribeContract();
        switch (mutationRisk)
        {
            case "mayDirty":
                describe.Assurance!.MayDirty = true;
                break;
            case "mayPersist":
                describe.Assurance!.MayPersist = true;
                break;
            case "sideEffects":
                describe.Assurance!.SideEffects = [UcliOperationSideEffectValues.SceneContentMutation];
                break;
        }

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            describe,
            operationKind: "query",
            operationPolicy: "safe",
            ownerName: "Test contract",
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has query assurance metadata with mutation or side-effect risk.", errorMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("advanced")]
    [InlineData("dangerous")]
    public void TryValidatePublicRawOpDescribeContract_WhenRiskyPolicyHasNoDangerousNotes_ReturnsFalse (
        string policy)
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = policy == "advanced"
            ? [UcliOperationSideEffectValues.EditorStateChange]
            : [UcliOperationSideEffectValues.ExternalProcess];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            describe,
            operationKind: "command",
            operationPolicy: policy,
            ownerName: "Test contract",
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract must declare dangerousNotes for advanced or dangerous policy.", errorMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliOperationSideEffectValues.EditorStateChange)]
    [InlineData(UcliOperationSideEffectValues.ExternalProcess)]
    public void TryValidatePublicRawOpDescribeContract_WhenDerivedRiskyPolicyHasNoDangerousNotes_ReturnsFalse (
        string sideEffect)
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [sideEffect];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract must declare dangerousNotes for advanced or dangerous policy.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenDeclaredPolicyDoesNotMatchDerivedPolicy_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [UcliOperationSideEffectValues.EditorStateChange];
        describe.Assurance.DangerousNotes = ["Editor state changes require advanced policy."];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            describe,
            operationKind: "command",
            operationPolicy: "safe",
            ownerName: "Test contract",
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract policy 'safe' does not match derived policy 'advanced'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractLacksArbitrarySourceExecution_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.CodeContract = CreateValidCodeContract();

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has invalid policy derivation metadata. Operations with codeContract must declare sideEffects value 'arbitrarySourceExecution'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenDangerousNoteIsEmpty_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.DangerousNotes = [" "];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid dangerous note.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractParameterDescriptionIsMissing_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [UcliOperationSideEffectValues.ArbitrarySourceExecution];
        describe.Assurance.DangerousNotes = ["Arbitrary source execution requires dangerous policy."];
        describe.CodeContract = new UcliOperationCodeContract(
            "csharp",
            new UcliCodeEntryPointContract(
                "public static object? Run(SampleContext context)",
                "Compiled source must contain exactly one matching Run method.",
                requiredStatic: true,
                new[] { "SampleContext" },
                "JSON-serializable value."),
            new[]
            {
                new UcliCodeSourceFormContract(CsEvalSourceKindValues.CompilationUnit, "Complete C# compilation unit."),
            },
            new[]
            {
                new UcliCodeApiTypeContract(
                    "SampleContext",
                    "SampleContext",
                    "Sample context.",
                    new[]
                    {
                        new UcliCodeApiMemberContract(
                            UcliCodeApiMemberKindValues.Method,
                            "Log",
                            "Records a log message.",
                            type: null,
                            returnType: "void",
                            parameters:
                            [
                                new UcliCodeApiParameterContract("message", "System.String", string.Empty),
                            ]),
                    }),
            });

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid codeContract method parameter at index 0.", errorMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractEntryPointMatchRuleIsMissing_ReturnsFalse (
        string? matchRule)
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [UcliOperationSideEffectValues.ArbitrarySourceExecution];
        describe.Assurance.DangerousNotes = ["Arbitrary source execution requires dangerous policy."];
        describe.CodeContract = CreateValidCodeContract();
        describe.CodeContract.EntryPoint!.MatchRule = matchRule;

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has invalid codeContract metadata.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractLanguageIsUnsupported_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [UcliOperationSideEffectValues.ArbitrarySourceExecution];
        describe.Assurance.DangerousNotes = ["Arbitrary source execution requires dangerous policy."];
        describe.CodeContract = CreateValidCodeContract();
        describe.CodeContract.Language = "python";

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an unsupported codeContract language.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractSourceFormIsUnsupported_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.Assurance!.SideEffects = [UcliOperationSideEffectValues.ArbitrarySourceExecution];
        describe.Assurance.DangerousNotes = ["Arbitrary source execution requires dangerous policy."];
        describe.CodeContract = CreateValidCodeContract();
        describe.CodeContract.SourceForms =
        [
            new UcliCodeSourceFormContract("not-supported", "Unsupported source form."),
        ];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an unsupported codeContract source form at index 0.", errorMessage);
    }

    private static UcliOperationDescribeContract CreateValidDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
            "Opens a Unity scene asset in the editor.",
            new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()));
    }

    public static IEnumerable<object[]> RequiredAssuranceFactFailureCases
    {
        get
        {
            yield return new object[]
            {
                UcliOperationSideEffectValues.AssetContentMutation,
                false,
                false,
                new[] { IpcExecuteTouchedResourceKindNames.Asset },
                "Test contract side effect 'assetContentMutation' requires assurance.mayDirty=true.",
            };
            yield return new object[]
            {
                UcliOperationSideEffectValues.AssetContentMutation,
                true,
                false,
                Array.Empty<string>(),
                "Test contract side effect 'assetContentMutation' requires assurance.touchedKinds to include 'asset'.",
            };
            yield return new object[]
            {
                UcliOperationSideEffectValues.AssetSave,
                false,
                false,
                new[] { IpcExecuteTouchedResourceKindNames.Asset },
                "Test contract side effect 'assetSave' requires assurance.mayPersist=true.",
            };
            yield return new object[]
            {
                UcliOperationSideEffectValues.AssetSave,
                false,
                true,
                Array.Empty<string>(),
                "Test contract side effect 'assetSave' requires assurance.touchedKinds to include 'asset'.",
            };
            yield return new object[]
            {
                UcliOperationSideEffectValues.FilesystemWrite,
                false,
                false,
                Array.Empty<string>(),
                "Test contract side effect 'filesystemWrite' requires assurance.mayPersist=true.",
            };
            yield return new object[]
            {
                UcliOperationSideEffectValues.OpensSceneInEditor,
                false,
                false,
                Array.Empty<string>(),
                "Test contract side effect 'opensSceneInEditor' requires assurance.touchedKinds to include 'scene'.",
            };
            yield return new object[]
            {
                UcliOperationSideEffectValues.ProjectSave,
                false,
                true,
                new[]
                {
                    IpcExecuteTouchedResourceKindNames.Scene,
                    IpcExecuteTouchedResourceKindNames.Prefab,
                    IpcExecuteTouchedResourceKindNames.Asset,
                },
                "Test contract side effect 'projectSave' requires assurance.touchedKinds to include 'projectSettings'.",
            };
        }
    }

    private static UcliOperationCodeContract CreateValidCodeContract ()
    {
        return new UcliOperationCodeContract(
            "csharp",
            new UcliCodeEntryPointContract(
                "public static object? Run(SampleContext context)",
                "Compiled source must contain exactly one matching Run method.",
                requiredStatic: true,
                new[] { "SampleContext" },
                "JSON-serializable value."),
            new[]
            {
                new UcliCodeSourceFormContract(CsEvalSourceKindValues.CompilationUnit, "Complete C# compilation unit."),
            },
            new[]
            {
                new UcliCodeApiTypeContract(
                    "SampleContext",
                    "SampleContext",
                    "Sample context.",
                    new[]
                    {
                        new UcliCodeApiMemberContract(
                            UcliCodeApiMemberKindValues.Method,
                            "Log",
                            "Records a log message.",
                            type: null,
                            returnType: "void",
                            parameters:
                            [
                                new UcliCodeApiParameterContract("message", "System.String", "Log message."),
                            ]),
                    }),
            });
    }
}
