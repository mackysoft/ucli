namespace MackySoft.Ucli.Configuration
{
    /// <summary> Defines plan-token requirements configured in <c>.ucli/config.json</c>. </summary>
    internal enum PlanTokenMode
    {
        /// <summary> Allows command execution with or without a plan token. </summary>
        Optional = 0,

        /// <summary> Requires command execution to include a plan token. </summary>
        Required = 1,
    }
}
