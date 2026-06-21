#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents the resolved build options passed to a uCLI build runner. </summary>
    public sealed class UcliBuildOptions
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildOptions" /> class. </summary>
        /// <param name="development"> Whether Unity development build output is requested. </param>
        public UcliBuildOptions (bool development)
        {
            Development = development;
        }

        /// <summary> Gets a value indicating whether Unity development build output is requested. </summary>
        public bool Development { get; }
    }
}
