using MackySoft.Ucli.Application.Diagnostics;

namespace MackySoft.Ucli.Application.Tests.Text;

public sealed class ApplicationVocabularyDefinitionTests
{
    private static readonly Type[] VocabularyTypes = typeof(ApplicationErrorCodeDescriptors).Assembly
        .GetTypes()
        .Where(static type => type.IsDefined(typeof(VocabularyDefinitionAttribute), inherit: false))
        .OrderBy(static type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_ForEveryApplicationVocabularyDefinition_Completes ()
    {
        Assert.NotEmpty(VocabularyTypes);

        foreach (Type vocabularyType in VocabularyTypes)
        {
            TextVocabulary.Validate(vocabularyType);
        }
    }
}
