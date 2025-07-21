using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Bed.Sleep;

// Forge-Change full (refactory b.y. wizard)

/// <summary>
/// Prevents waking up. Use only in conjunction with <see cref="StatusEffectComponent"/>, on the status effect entity.
/// </summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class ForcedSleepingStatusEffectComponent : Component;
