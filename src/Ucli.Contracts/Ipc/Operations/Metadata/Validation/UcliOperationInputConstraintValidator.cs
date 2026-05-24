using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates operation input constraint metadata entries. </summary>
internal static class UcliOperationInputConstraintValidator
{
    private static readonly HashSet<string> ParameterlessKinds = new(StringComparer.Ordinal)
    {
        UcliOperationInputConstraintKindValues.NonEmpty,
        UcliOperationInputConstraintKindValues.ProjectRelativePath,
        UcliOperationInputConstraintKindValues.GlobalObjectId,
        UcliOperationInputConstraintKindValues.HierarchyPath,
        UcliOperationInputConstraintKindValues.TypeExists,
        UcliOperationInputConstraintKindValues.AssetGuid,
        UcliOperationInputConstraintKindValues.Cursor,
    };

    private static readonly HashSet<string> SupportedAssetKinds = new(StringComparer.Ordinal)
    {
        UcliOperationAssetKindValues.Asset,
        UcliOperationAssetKindValues.Prefab,
        UcliOperationAssetKindValues.ProjectSettings,
        UcliOperationAssetKindValues.Scene,
    };

    private static readonly HashSet<string> SupportedReferenceTargetKinds = new(StringComparer.Ordinal)
    {
        UcliOperationReferenceTargetKindValues.Asset,
        UcliOperationReferenceTargetKindValues.Component,
        UcliOperationReferenceTargetKindValues.GameObject,
    };

    public static bool TryValidate (
        UcliOperationInputConstraintContract? constraint,
        out string errorMessage)
    {
        if (constraint == null || string.IsNullOrWhiteSpace(constraint.Kind))
        {
            errorMessage = "Constraint kind is missing.";
            return false;
        }

        return TryValidateKnownConstraint(constraint, out errorMessage);
    }

    private static bool TryValidateKnownConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (ParameterlessKinds.Contains(constraint.Kind!))
        {
            return TryValidateNoConstraintParameters(constraint, out errorMessage);
        }

        return constraint.Kind switch
        {
            UcliOperationInputConstraintKindValues.Range => TryValidateRangeConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKindValues.AssetExists => TryValidateAssetKindConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKindValues.AssetCreatable => TryValidateAssetKindConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKindValues.ReferenceResolvable => TryValidateReferenceTargetKindConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKindValues.TypeAssignableTo => TryValidateTypeKindConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKindValues.SerializedProperty => TryValidateSerializedPropertyConstraint(constraint, out errorMessage),
            _ => UnsupportedConstraint(constraint.Kind!, out errorMessage),
        };
    }

    private static bool UnsupportedConstraint (
        string kind,
        out string errorMessage)
    {
        errorMessage = $"Unsupported constraint kind '{kind}'.";
        return false;
    }

    private static bool TryValidateNoConstraintParameters (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (HasAnyParameter(constraint))
        {
            errorMessage = "Constraint has unsupported parameters.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateRangeConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || (constraint.Min == null && constraint.Max == null))
        {
            errorMessage = "Range constraint must only define min, max, or both.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateAssetKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (!SupportedAssetKinds.Contains(constraint.AssetKind ?? string.Empty)
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Asset constraint must define one supported asset kind.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateReferenceTargetKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (!SupportedReferenceTargetKinds.Contains(constraint.TargetKind ?? string.Empty)
            || constraint.AssetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Reference constraint must define one supported target kind.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateTypeKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (!string.Equals(constraint.TypeKind, UcliOperationTypeKindValues.Component, StringComparison.Ordinal)
            || constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Type assignability constraint must define one supported type kind.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateSerializedPropertyConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (!string.Equals(constraint.Access, UcliOperationSerializedPropertyAccessValues.Write, StringComparison.Ordinal)
            || constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Serialized-property constraint must define one supported access value.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool HasAnyParameter (UcliOperationInputConstraintContract constraint)
    {
        return constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null;
    }
}
