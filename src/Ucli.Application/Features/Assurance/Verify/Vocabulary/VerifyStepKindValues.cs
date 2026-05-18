namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

/// <summary> Defines closed verify profile step kind values. </summary>
internal static class VerifyStepKindValues
{
    public const string Ready = "ready";
    public const string Compile = "compile";
    public const string PostRead = "postRead";
    public const string Test = "test";
    public const string Logs = "logs";

    public static IReadOnlyList<string> CanonicalOrder { get; } =
    [
        Ready,
        Compile,
        PostRead,
        Test,
        Logs,
    ];

    public static bool IsSupported (string kind)
    {
        return kind is Ready or Compile or PostRead or Test or Logs;
    }
}
