using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexOpsDescribeJsonWriterSchemaTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Writer_EmitsSchemaObjectsAndOmitsRawSchemaJsonFields ()
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateGoDescribeIndexContract();
        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);

        using var jsonDocument = JsonDocument.Parse(json);
        var operationElement = jsonDocument.RootElement.GetProperty("operation");
        Assert.False(operationElement.TryGetProperty("argsSchemaJson", out _));
        Assert.False(operationElement.TryGetProperty("resultSchemaJson", out _));
        JsonAssert.For(operationElement)
            .HasProperty("argsSchema", schema => schema
                .HasString("type", "object"))
            .HasProperty("resultSchema", schema => schema
                .HasString("type", "object"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Writer_EmitsVariantFieldConstraintsAndOmitsLegacyVariantLevelFields ()
    {
        var contract = IndexOpsDescribeJsonContractTestSupport.CreateGoDescribeIndexContract();
        var json = new IndexOpsDescribeJsonContractWriter().Write(contract);

        using var jsonDocument = JsonDocument.Parse(json);
        var operationElement = jsonDocument.RootElement.GetProperty("operation");
        var targetInputElement = operationElement.GetProperty("inputs").EnumerateArray().Single(input =>
            string.Equals(input.GetProperty("name").GetString(), "target", StringComparison.Ordinal));
        var globalObjectIdVariantElement = targetInputElement.GetProperty("variants").EnumerateArray().Single(variant =>
            string.Equals(variant.GetProperty("name").GetString(), "byGlobalObjectId", StringComparison.Ordinal));

        Assert.False(globalObjectIdVariantElement.TryGetProperty("argsPaths", out _));
        Assert.False(globalObjectIdVariantElement.TryGetProperty("constraints", out _));
        JsonAssert.For(Assert.Single(globalObjectIdVariantElement.GetProperty("fields").EnumerateArray()))
            .HasString("name", "globalObjectId")
            .HasString("argsPath", "$.target.globalObjectId")
            .HasString("description", "Resolved Unity GlobalObjectId.")
            .HasArrayLength("constraints", 1)
            .HasProperty("constraints", 0, constraint => constraint
                .HasString("kind", "globalObjectId"));

        var sceneHierarchyVariantElement = targetInputElement.GetProperty("variants").EnumerateArray().Single(variant =>
            string.Equals(variant.GetProperty("name").GetString(), "bySceneHierarchyPath", StringComparison.Ordinal));
        var sceneFieldElement = sceneHierarchyVariantElement.GetProperty("fields").EnumerateArray().Single(candidate =>
            string.Equals(candidate.GetProperty("name").GetString(), "scene", StringComparison.Ordinal));
        var hierarchyPathFieldElement = sceneHierarchyVariantElement.GetProperty("fields").EnumerateArray().Single(candidate =>
            string.Equals(candidate.GetProperty("name").GetString(), "hierarchyPath", StringComparison.Ordinal));

        Assert.False(sceneHierarchyVariantElement.TryGetProperty("argsPaths", out _));
        Assert.False(sceneHierarchyVariantElement.TryGetProperty("constraints", out _));
        JsonAssert.For(sceneFieldElement)
            .HasString("argsPath", "$.target.scene")
            .HasString("description", "Scene asset path for a hierarchy selector.");
        var assetExistsConstraint = sceneFieldElement.GetProperty("constraints").EnumerateArray().Single(constraint =>
            string.Equals(constraint.GetProperty("kind").GetString(), "assetExists", StringComparison.Ordinal));
        JsonAssert.For(assetExistsConstraint)
            .HasString("assetKind", "scene");
        JsonAssert.For(hierarchyPathFieldElement)
            .HasString("argsPath", "$.target.hierarchyPath")
            .HasString("description", "Unity hierarchy path inside the selected scene or prefab.");
        Assert.Contains(hierarchyPathFieldElement.GetProperty("constraints").EnumerateArray(), constraint =>
            string.Equals(constraint.GetProperty("kind").GetString(), "hierarchyPath", StringComparison.Ordinal));
    }
}
