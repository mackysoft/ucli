using System;
using MackySoft.Ucli.Contracts.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Represents one index-catalog build result with generated contracts or one failure reason. </summary>
    /// <param name="TypesCatalog"> The generated <c>types.catalog.json</c> contract on success. </param>
    /// <param name="SchemasCatalog"> The generated <c>schemas.catalog.json</c> contract on success. </param>
    /// <param name="InputsManifest"> The generated <c>inputs/manifest.json</c> contract on success. </param>
    /// <param name="ErrorMessage"> The error message on failure; otherwise <see langword="null" />. </param>
    internal sealed record IndexCatalogBuildResult (
        IndexTypesCatalogJsonContract? TypesCatalog,
        IndexSchemasCatalogJsonContract? SchemasCatalog,
        IndexInputsManifestJsonContract? InputsManifest,
        string? ErrorMessage)
    {
        /// <summary> Gets a value indicating whether catalog build succeeded. </summary>
        public bool IsSuccess => TypesCatalog != null
            && SchemasCatalog != null
            && InputsManifest != null
            && string.IsNullOrWhiteSpace(ErrorMessage);

        /// <summary> Creates one successful build result. </summary>
        /// <param name="typesCatalog"> The generated types catalog contract. </param>
        /// <param name="schemasCatalog"> The generated schemas catalog contract. </param>
        /// <param name="inputsManifest"> The generated inputs manifest contract. </param>
        /// <returns> The successful result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any argument is <see langword="null" />. </exception>
        public static IndexCatalogBuildResult Success (
            IndexTypesCatalogJsonContract typesCatalog,
            IndexSchemasCatalogJsonContract schemasCatalog,
            IndexInputsManifestJsonContract inputsManifest)
        {
            if (typesCatalog == null)
            {
                throw new ArgumentNullException(nameof(typesCatalog));
            }

            if (schemasCatalog == null)
            {
                throw new ArgumentNullException(nameof(schemasCatalog));
            }

            if (inputsManifest == null)
            {
                throw new ArgumentNullException(nameof(inputsManifest));
            }

            return new IndexCatalogBuildResult(typesCatalog, schemasCatalog, inputsManifest, null);
        }

        /// <summary> Creates one failed build result. </summary>
        /// <param name="errorMessage"> The failure reason text. </param>
        /// <returns> The failed result. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="errorMessage" /> is <see langword="null" />, empty, or whitespace. </exception>
        public static IndexCatalogBuildResult Failure (string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Error message must not be empty.", nameof(errorMessage));
            }

            return new IndexCatalogBuildResult(null, null, null, errorMessage);
        }
    }
}