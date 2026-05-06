namespace MackySoft.Ucli.Contracts.Json;

/// <summary> Writes one contract object to its public JSON representation. </summary>
/// <typeparam name="TContract"> The contract type written by this writer. </typeparam>
internal interface IJsonContractWriter<in TContract>
{
    /// <summary> Writes the contract as formatted JSON with LF line endings and one trailing newline. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> The public JSON text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="contract" /> is <see langword="null" />. </exception>
    string Write (TContract contract);
}
