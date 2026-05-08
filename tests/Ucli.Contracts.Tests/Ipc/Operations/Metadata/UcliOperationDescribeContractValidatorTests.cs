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
        describe.CodeContract = CreateValidCodeContract();

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractParameterDescriptionIsMissing_ReturnsFalse ()
    {
        var describe = CreateValidDescribeContract();
        describe.CodeContract = new UcliOperationCodeContract(
            "csharp",
            new UcliCodeEntryPointContract(
                "public static object? Run(SampleContext context)",
                requiredStatic: true,
                new[] { "SampleContext" },
                "JSON-serializable value."),
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

    private static UcliOperationDescribeContract CreateValidDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
            "Opens a Unity scene asset in the editor.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));
    }

    private static UcliOperationCodeContract CreateValidCodeContract ()
    {
        return new UcliOperationCodeContract(
            "csharp",
            new UcliCodeEntryPointContract(
                "public static object? Run(SampleContext context)",
                requiredStatic: true,
                new[] { "SampleContext" },
                "JSON-serializable value."),
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
