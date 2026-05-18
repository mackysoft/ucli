namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

/// <summary> Defines claim codes emitted by the verify command. </summary>
internal static class VerifyClaimCodes
{
    /// <summary> Gets the claim that touched persistence units were observed. </summary>
    public static readonly UcliCode PersistenceUnitTouched = new("PERSISTENCE_UNIT_TOUCHED");

    /// <summary> Gets the claim that post-mutation read surfaces have safety requirements. </summary>
    public static readonly UcliCode ReadSurfaceSafe = new("READ_SURFACE_SAFE");

    /// <summary> Gets the claim that expected post-mutation state was observed. </summary>
    public static readonly UcliCode PostMutationObserved = new("POST_MUTATION_OBSERVED");

    /// <summary> Gets the claim that Unity tests passed. </summary>
    public static readonly UcliCode UnityTestsPassed = new("UNITY_TESTS_PASSED");

    /// <summary> Gets all verify-owned claim codes. </summary>
    public static IReadOnlyList<UcliCode> All { get; } =
    [
        PersistenceUnitTouched,
        ReadSurfaceSafe,
        PostMutationObserved,
        UnityTestsPassed,
    ];
}
