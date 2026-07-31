namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary>
/// Declares the Play Mode outcome subset guaranteed by one typed result property.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UcliPlayTransitionOutcomeSubsetAttribute : Attribute
{
    /// <summary> Initializes one typed Play Mode outcome-subset declaration. </summary>
    /// <param name="subset"> The subset guaranteed by the declaring result property. </param>
    public UcliPlayTransitionOutcomeSubsetAttribute (
        UcliPlayTransitionOutcomeSubset subset)
    {
        if (!Enum.IsDefined(typeof(UcliPlayTransitionOutcomeSubset), subset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(subset),
                subset,
                "Play Mode outcome subset must be defined.");
        }

        Subset = subset;
    }

    /// <summary> Gets the subset guaranteed by the declaring result property. </summary>
    public UcliPlayTransitionOutcomeSubset Subset { get; }
}
