using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates operation input constraint metadata entries. </summary>
internal static class UcliOperationInputConstraintValidator
{
    private static readonly HashSet<string> ParameterlessKinds = new(StringComparer.Ordinal)
    {
        TextVocabulary.GetText(UcliOperationInputConstraintKind.NonEmpty),
        TextVocabulary.GetText(UcliOperationInputConstraintKind.ProjectRelativePath),
        TextVocabulary.GetText(UcliOperationInputConstraintKind.GlobalObjectId),
        TextVocabulary.GetText(UcliOperationInputConstraintKind.HierarchyPath),
        TextVocabulary.GetText(UcliOperationInputConstraintKind.TypeExists),
        TextVocabulary.GetText(UcliOperationInputConstraintKind.AssetGuid),
        TextVocabulary.GetText(UcliOperationInputConstraintKind.Cursor),
    };

    private static readonly HashSet<string> SupportedAssetKinds = new(StringComparer.Ordinal)
    {
        TextVocabulary.GetText(UcliOperationAssetKind.Asset),
        TextVocabulary.GetText(UcliOperationAssetKind.Prefab),
        TextVocabulary.GetText(UcliOperationAssetKind.ProjectSettings),
        TextVocabulary.GetText(UcliOperationAssetKind.Scene),
    };

    private static readonly HashSet<string> SupportedReferenceTargetKinds = new(StringComparer.Ordinal)
    {
        TextVocabulary.GetText(UcliOperationReferenceTargetKind.Asset),
        TextVocabulary.GetText(UcliOperationReferenceTargetKind.Component),
        TextVocabulary.GetText(UcliOperationReferenceTargetKind.GameObject),
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
        if (!TextVocabulary.TryGetValue<UcliOperationInputConstraintKind>(constraint.Kind, out var kind))
        {
            return UnsupportedConstraint(constraint.Kind!, out errorMessage);
        }

        if (ParameterlessKinds.Contains(constraint.Kind!))
        {
            return TryValidateNoConstraintParameters(constraint, out errorMessage);
        }

        return kind switch
        {
            UcliOperationInputConstraintKind.Range => TryValidateRangeConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKind.AssetExists => TryValidateAssetKindConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKind.AssetCreatable => TryValidateAssetKindConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKind.ReferenceResolvable => TryValidateReferenceTargetKindConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKind.TypeAssignableTo => TryValidateTypeKindConstraint(constraint, out errorMessage),
            UcliOperationInputConstraintKind.SerializedProperty => TryValidateSerializedPropertyConstraint(constraint, out errorMessage),
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
        if (!TextVocabulary.Matches(constraint.TypeKind, UcliOperationTypeKind.Component)
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
        if (!TextVocabulary.Matches(constraint.Access, UcliOperationSerializedPropertyAccess.Write)
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
