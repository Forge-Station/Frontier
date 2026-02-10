using Content.Shared._NF.Bank.Components;
using Content.Shared.Cargo;
using Robust.Shared.GameStates;

namespace Content.Shared._Mono.ItemTax.Components;

/// <summary>
/// This is used to add or substract additional money to a budget when a specific item is sold.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ItemTaxComponent : Component
{
    /// <summary>
    /// Defines the percent tax to be added to or taken from each budget on pallet crate sell.
    /// </summary>
    [DataField]
    public Dictionary<SectorBankAccount, float> TaxAccounts = new();
}
