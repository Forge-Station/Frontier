// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goobstation.Clothing.Components;
/// <summary>
/// Component applied by clothing that allows the wearer to inject themselves with a reagent on a cooldown.
/// Used for auto-injection mechanisms like emergency epi-pens or stimulants. Possible uses for a modsuit in the future.
/// </summary>
[RegisterComponent]
public sealed partial class ClothingAutoInjectComponent : Component
{
    /// <summary>
    /// Dictionary of reagents and their quantities to be injected.
    /// Key: Reagent ID, Value: Quantity to inject.
    /// </summary>
    [DataField("reagents", required: true)]
    public List<ReagentQuantity> Reagents = new();

    [DataField]
    public bool AutoInjectOnCrit = true;

    /// <summary>
    /// Can this autoinjector be activated manually?
    /// </summary>
    [DataField]
    public bool AutoInjectOnAbility;

    [DataField]
    public TimeSpan AutoInjectInterval = TimeSpan.FromSeconds(120);

    /// <summary>
    /// When the auto-injector can activate again.
    /// </summary>
    public TimeSpan NextAutoInjectTime;

    /// <summary>
    /// The ID of the action used to activate the auto-injector.
    /// </summary>
    [DataField]
    public EntProtoId Action = "ActionActivateAutoinjector";

    [DataField]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    [DataField]
    public LocId Popup = "autoinjector-injection-hardsuit";

    [ViewVariables]
    public EntityUid? ActionEntity;
}


[DataDefinition]
public sealed partial class ReagentQuantity
{
    [DataField("reagent", required: true)]
    public string Reagent = default!;

    [DataField("quantity", required: true)]
    public FixedPoint2 Quantity;
}
