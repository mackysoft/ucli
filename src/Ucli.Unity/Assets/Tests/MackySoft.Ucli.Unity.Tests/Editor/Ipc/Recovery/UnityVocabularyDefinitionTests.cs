using System;
using System.Linq;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityVocabularyDefinitionTests
    {
        [Test]
        public void Validate_ForEveryUnityVocabularyDefinition_Completes ()
        {
            Type[] vocabularyTypes = typeof(RecoverableIpcOperationState).Assembly
                .GetTypes()
                .Where(type => type.IsDefined(typeof(VocabularyDefinitionAttribute), inherit: false))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            Assert.That(vocabularyTypes, Is.Not.Empty);
            foreach (Type vocabularyType in vocabularyTypes)
            {
                TextVocabulary.Validate(vocabularyType);
            }
        }
    }
}
