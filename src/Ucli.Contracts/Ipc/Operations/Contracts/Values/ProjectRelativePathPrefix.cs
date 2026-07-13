using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Non-empty project-relative path prefix used to filter Unity assets. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Non-empty project-relative path prefix used to filter Unity assets.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
public sealed record ProjectRelativePathPrefix : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="ProjectRelativePathPrefix" /> class. </summary>
    /// <param name="value"> The project-relative path prefix. </param>
    [JsonConstructor]
    public ProjectRelativePathPrefix (string value)
        : base(value)
    {
    }
}
