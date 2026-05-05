namespace MackySoft.Ucli.Application.Features.Init.UseCases.Init;

/// <summary> Represents normalized CLI input values for one init execution. </summary>
/// <param name="Force"> Whether existing template files can be overwritten. </param>
internal sealed record InitCommandInput (bool Force);
