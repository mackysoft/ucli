using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents an expected available or unavailable outcome from one Unity asset read boundary. </summary>
    /// <typeparam name="T"> The value available when the boundary completed normally. </typeparam>
    internal sealed class MissingScriptsAssetAccessOutcome<T>
    {
        private readonly T? value;

        private MissingScriptsAssetAccessOutcome (bool isAvailable, T? value)
        {
            IsAvailable = isAvailable;
            this.value = value;
        }

        /// <summary> Gets whether the requested Unity asset fact was available. </summary>
        public bool IsAvailable { get; }

        /// <summary> Gets the available value. </summary>
        /// <exception cref="InvalidOperationException"> The outcome is unavailable. </exception>
        public T Value => IsAvailable
            ? value!
            : throw new InvalidOperationException("An unavailable missing-script asset access outcome has no value.");

        /// <summary> Creates one available asset access outcome. </summary>
        /// <param name="value"> The observed value. </param>
        /// <returns> The available outcome. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="value" /> is <see langword="null" />. </exception>
        public static MissingScriptsAssetAccessOutcome<T> Available (T value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new MissingScriptsAssetAccessOutcome<T>(
                isAvailable: true,
                value: value);
        }

        /// <summary> Creates one expected unavailable asset access outcome. </summary>
        /// <returns> The unavailable outcome. </returns>
        public static MissingScriptsAssetAccessOutcome<T> Unavailable ()
        {
            return new MissingScriptsAssetAccessOutcome<T>(isAvailable: false, value: default);
        }
    }
}
