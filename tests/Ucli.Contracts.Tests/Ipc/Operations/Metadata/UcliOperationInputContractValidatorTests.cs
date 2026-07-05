using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationInputContractValidatorTests
{
    private static readonly InputConstraintFailureCase[] InputConstraintFailureCases =
    [
        new(
            new UcliOperationInputConstraintContract("nonEmpty")
            {
                Min = 1,
            },
            "Constraint has unsupported parameters."),
        new(
            new UcliOperationInputConstraintContract("range"),
            "Range constraint must only define min, max, or both."),
        new(
            new UcliOperationInputConstraintContract("assetExists"),
            "Asset constraint must define one supported asset kind."),
        new(
            new UcliOperationInputConstraintContract("assetCreatable")
            {
                AssetKind = "unsupported",
            },
            "Asset constraint must define one supported asset kind."),
        new(
            new UcliOperationInputConstraintContract("referenceResolvable"),
            "Reference constraint must define one supported target kind."),
        new(
            new UcliOperationInputConstraintContract("typeAssignableTo")
            {
                TypeKind = "unsupported",
            },
            "Type assignability constraint must define one supported type kind."),
        new(
            new UcliOperationInputConstraintContract("serializedProperty"),
            "Serialized-property constraint must define one supported access value."),
        new(
            new UcliOperationInputConstraintContract("unsupported"),
            "Unsupported constraint kind 'unsupported'."),
    ];

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
                                    new UcliOperationInputConstraintContract("assetExists")
                                    {
                                        AssetKind = "scene",
                                    },
                                }),
                            new UcliOperationInputVariantFieldContract(
                                "hierarchyPath",
                                "$.target.hierarchyPath",
                                "Hierarchy path.",
                                new[]
                                {
                                    new UcliOperationInputConstraintContract("hierarchyPath"),
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
    public void TryValidatePublicRawOpInputs_WhenInputConstraintShapeIsInvalid_ReturnsFalse ()
    {
        foreach (var testCase in InputConstraintFailureCases)
        {
            var inputs = new[]
            {
                new UcliOperationInputContract(
                    "target",
                    "string",
                    "Target value.",
                    new[] { testCase.Constraint }),
            };

            var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpInputs(inputs, "Test contract", out var errorMessage);

            Assert.False(isValid);
            Assert.Equal($"Test contract has an invalid input constraint at index 0. {testCase.ExpectedConstraintError}", errorMessage);
        }
    }

    private sealed record InputConstraintFailureCase (
        UcliOperationInputConstraintContract Constraint,
        string ExpectedConstraintError);
}
