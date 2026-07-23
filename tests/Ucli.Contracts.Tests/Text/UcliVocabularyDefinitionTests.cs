using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class UcliVocabularyDefinitionTests
{
    private static readonly Type[] VocabularyTypes = typeof(OperationPolicy).Assembly
        .GetTypes()
        .Where(static type => type.IsDefined(typeof(VocabularyDefinitionAttribute), inherit: false))
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_ForEveryContractsVocabularyDefinition_Completes ()
    {
        Assert.NotEmpty(VocabularyTypes);

        foreach (Type vocabularyType in VocabularyTypes)
        {
            TextVocabulary.Validate(vocabularyType);
        }
    }
}
