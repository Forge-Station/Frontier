using Content.Shared.Atmos;

namespace Content.Server._Goobstation.Temperature;

public sealed class TemperatureImmunityEvent(float currentTemperature) : EntityEventArgs
{
    public float CurrentTemperature = currentTemperature;
    public readonly float IdealTemperature = Atmospherics.T37C;
}
