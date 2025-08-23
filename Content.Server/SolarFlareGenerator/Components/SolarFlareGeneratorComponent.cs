using Content.Server.Solar.EntitySystems;

/// <summary>
/// Стационарное устройство, при активации которого на срок в 30-60 секунд в радиусе 100-150 происходит эффект "Солнечной вспышки"
/// </summary>
[RegisterComponent]
[Access(typeof(SolarFlareGeneratorSystem))]
public sealed partial class SolarFlareGeneratorComponent : Component
{
    [DataField]
    public bool IsActive = false;
    [DataField]
    public bool IsReady = true;
    [DataField]
    public TimeSpan EffectTimer = TimeSpan.Zero;
    [DataField]
    public TimeSpan CooldownTimer = TimeSpan.Zero;
    [DataField]
    public float Radius;
}
