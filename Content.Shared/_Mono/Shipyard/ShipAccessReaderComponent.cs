// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 Redrover1760
// SPDX-FileCopyrightText: 2025 ark1368
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Shipyard;

/// <summary>
/// Component that marks an entity as requiring ship deed access.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShipAccessReaderComponent : Component
{
    /// <summary>
    /// Whether ship access checking is enabled for this entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    /// Message to show when access is denied due to lack of ship deed.
    /// </summary>
    [DataField]
    public string DeniedMessage = "ship-access-denied";

    /// <summary>
    /// Whether to show a popup when access is denied.
    /// </summary>
    [DataField]
    public bool ShowDeniedPopup = true;
}
