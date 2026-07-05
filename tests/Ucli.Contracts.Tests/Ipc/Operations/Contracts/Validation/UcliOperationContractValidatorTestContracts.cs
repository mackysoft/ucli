using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

internal static class UcliOperationContractValidatorTestContracts
{
    internal sealed record RequiredStringArgs (
        [property: UcliRequired]
        [property: UcliDescription("Required string.")]
        string? Name);

    internal sealed record RequiredArrayArgs (
        [property: UcliRequired]
        [property: UcliDescription("Required array.")]
        IReadOnlyList<string> Items);

    internal sealed record NonEmptyStringArgs (
        [property: UcliDescription("Name.")]
        [property: UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
        string? Name);

    internal sealed record NonEmptySemanticStringArgs (
        [property: UcliDescription("Path prefix.")]
        UnityHierarchyPathPrefix? PathPrefix);

    internal sealed record NonEmptyArrayArgs (
        [property: UcliDescription("Items.")]
        [property: UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
        IReadOnlyList<string> Items);

    internal sealed record NonEmptyJsonObjectArgs (
        [property: UcliDescription("Value.")]
        [property: UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
        JsonElement Value);

    internal sealed record RangeArgs (
        [property: UcliDescription("Depth.")]
        [property: UcliInputConstraint(UcliOperationInputConstraintKind.Range, Min = 0)]
        int Depth);

    internal sealed record CursorArgs (
        [property: UcliDescription("Cursor.")]
        [property: UcliInputConstraint(UcliOperationInputConstraintKind.Cursor)]
        string? Cursor);

    [UcliExclusiveRequiredPropertySet("scene")]
    [UcliExclusiveRequiredPropertySet("parent")]
    internal sealed record RequiredPropertySetArgs (
        [property: UcliDescription("Scene path.")]
        string? Scene,

        [property: UcliDescription("Parent hierarchy path.")]
        string? Parent);

    [UcliExclusiveRequiredPropertySet("globalObjectId")]
    [UcliExclusiveRequiredPropertySet("scene", "hierarchyPath")]
    internal sealed record SelectorRequiredPropertySetArgs (
        [property: UcliDescription("GlobalObjectId.")]
        string? GlobalObjectId,

        [property: UcliDescription("Scene path.")]
        string? Scene,

        [property: UcliDescription("Hierarchy path.")]
        string? HierarchyPath);

    [UcliPropertyRequires("componentType", "scene", "hierarchyPath")]
    internal sealed record PropertyRequiresArgs (
        [property: UcliDescription("Scene path.")]
        string? Scene,

        [property: UcliDescription("Hierarchy path.")]
        string? HierarchyPath,

        [property: UcliDescription("Component type.")]
        string? ComponentType);

    internal sealed record ReferenceArgs (
        [property: UcliDescription("Target GameObject reference.")]
        GameObjectReferenceArgs? Target);

    internal sealed record AnyJsonValueArgs (
        [property: UcliDescription("Arbitrary JSON value.")]
        [property: UcliJsonAnyValue]
        JsonElement Value);

    internal sealed record ReservedVarArgs (
        [property: UcliDescription("Reserved property name.")]
        [property: JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
        string? Alias);

    internal sealed record TopLevelPlanAliasVarArgs (
        [property: UcliDescription("Request-local alias.")]
        [property: JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
        UcliPlanAlias? Alias);

    internal sealed record CustomPlanAliasArgs (
        [property: UcliDescription("Request-local alias.")]
        UcliPlanAlias? Alias);

    internal sealed record CustomReferenceLikeArgs (
        [property: UcliDescription("Custom reference-like target.")]
        CustomReferenceLikeSelector? Target);

    internal sealed record CustomReferenceLikeSelector (
        [property: UcliDescription("Request-local alias.")]
        [property: JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
        UcliPlanAlias? Alias);
}
