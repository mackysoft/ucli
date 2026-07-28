using System.Reflection;
using System.Text.Json;
using MackySoft.JsonSchema.Generation.ContractModel;
using MackySoft.JsonSchema.Generation.Extensibility;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary> Contributes uCLI-specific semantic annotations declared on actual contract types and members. </summary>
internal sealed class UcliSemanticAnnotationContractModelContributor : IJsonContractModelContributor
{
    private const string SourceId = "mackysoft.ucli.semantic-annotations";

    public string StableId => SourceId;

    public string ContractVersion => "1";

    public IReadOnlyList<JsonContractModelContribution> GetContributions (JsonContractModelContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var contributions = new List<JsonContractModelContribution>();
        VisitNode(context.Root, context, contributions);
        for (var i = 0; i < context.Definitions.Count; i++)
        {
            VisitNode(context.Definitions[i].Value, context, contributions);
        }

        return contributions;
    }

    private static void VisitNode (
        JsonContractNode node,
        JsonContractModelContext context,
        ICollection<JsonContractModelContribution> contributions)
    {
        if (node.Kind != JsonContractNodeKind.Reference)
        {
            AddContributions(
                node.Source.TargetType,
                context.GetTarget(node),
                contributions);
        }

        if (node.Items != null)
        {
            VisitNode(node.Items, context, contributions);
        }

        if (node.AdditionalProperties != null)
        {
            VisitNode(node.AdditionalProperties, context, contributions);
        }

        for (var i = 0; i < node.Properties.Count; i++)
        {
            var property = node.Properties[i];
            if (property.Source.Member != null)
            {
                AddContributions(
                    property.Source.Member,
                    context.GetTarget(property),
                    contributions);
            }

            VisitNode(property.Value, context, contributions);
        }

        for (var i = 0; i < node.Variants.Count; i++)
        {
            VisitNode(node.Variants[i].Value, context, contributions);
        }
    }

    private static void AddContributions (
        ICustomAttributeProvider source,
        JsonContractModelTarget target,
        ICollection<JsonContractModelContribution> contributions)
    {
        var attributes = source.GetCustomAttributes(
            typeof(UcliOperationInputConstraintAnnotationAttribute),
            inherit: true);
        for (var i = 0; i < attributes.Length; i++)
        {
            var declaration = CreateDeclaration((UcliOperationInputConstraintAnnotationAttribute)attributes[i]);
            contributions.Add(new JsonContractModelContribution(
                target,
                declaration.Name,
                declaration.Value,
                SourceId));
        }
    }

    private static ContributionDeclaration CreateDeclaration (
        UcliOperationInputConstraintAnnotationAttribute attribute)
    {
        return attribute switch
        {
            UcliAssetCreatableAttribute value => CreateVocabularyDeclaration(
                "ucli.assetCreatable",
                value.AssetKind),
            UcliAssetExistsAttribute value => CreateVocabularyDeclaration(
                "ucli.assetExists",
                value.AssetKind),
            UcliReferenceResolvableAttribute value => CreateVocabularyDeclaration(
                "ucli.referenceResolvable",
                value.TargetKind),
            UcliSerializedPropertyAttribute value => CreateVocabularyDeclaration(
                "ucli.serializedProperty",
                value.Access),
            UcliTypeAssignableToAttribute value => CreateVocabularyDeclaration(
                "ucli.typeAssignableTo",
                value.TypeKind),
            _ => CreateMarkerDeclaration(attribute),
        };
    }

    private static ContributionDeclaration CreateMarkerDeclaration (
        UcliOperationInputConstraintAnnotationAttribute attribute)
    {
        var name = attribute switch
        {
            UcliAssetGuidAttribute => "ucli.assetGuid",
            UcliCursorAttribute => "ucli.cursor",
            UcliGlobalObjectIdAttribute => "ucli.globalObjectId",
            UcliHierarchyPathAttribute => "ucli.hierarchyPath",
            UcliProjectRelativePathAttribute => "ucli.projectRelativePath",
            UcliTypeExistsAttribute => "ucli.typeExists",
            _ => throw new InvalidOperationException(
                $"Unsupported uCLI semantic annotation type '{attribute.GetType().FullName}'."),
        };
        return CreateMarkerDeclaration(name);
    }

    private static ContributionDeclaration CreateMarkerDeclaration (string name)
    {
        return new ContributionDeclaration(
            name,
            JsonSerializer.SerializeToElement(true));
    }

    private static ContributionDeclaration CreateVocabularyDeclaration<TVocabulary> (
        string name,
        TVocabulary value)
        where TVocabulary : struct, Enum
    {
        return new ContributionDeclaration(
            name,
            JsonSerializer.SerializeToElement(TextVocabulary.GetText(value)));
    }

    private readonly record struct ContributionDeclaration (
        string Name,
        JsonElement Value);
}
