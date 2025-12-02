// SPDX-FileCopyrightText: 2025 bitcrushing
//
// SPDX-License-Identifier: MPL-2.0

using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Hitscan.Components;

/// <summary>
/// Entities with this component will cause hitscan beams to diffract when hit
/// Allows any entity i.e. glass to trigger diffraction
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanDiffractTargetComponent : Component
{
    /// <summary>
    /// Used to toggle diffraction behaviour on/off i.e. if entity becomes damaged
    /// </summary>
    [DataField]
    public bool Active = true;

}
