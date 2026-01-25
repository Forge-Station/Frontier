using Content.Shared._NF.Bank.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._NF.Bank.BUI;

[Serializable, NetSerializable]
public sealed class NFLedgerState : BoundUserInterfaceState
{
    public readonly NFLedgerEntry[] Entries;
    public NFLedgerState(NFLedgerEntry[] entries)
    {
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public struct NFLedgerEntry
{
    public SectorBankAccount Account;
    public LedgerEntryType Type;
    public int Amount;
}

public enum LedgerEntryType : byte
{
    // Income entries
    TickingIncome,
    VendorTax,
    CargoTax,
    MailDelivered,
    ShipyardRenameFee,
    // Forge-change-start take Mono taxes
    // BlackMarketAtmTax,
    // BlackMarketShipyardTax,
    AtmTax,
    ShipyardTax,
    // Mono begin
    BlackMarketSales,
    FrontierSales,
    NFSDSales,
    MedicalSales,
    // Forge-change-end take Mono taxes
    BluespaceReward,
    AntiSmugglingBonus,
    MedicalBountyTax,
    PowerTransmission,
    StationDepositFines,
    StationDepositDonation,
    StationDepositAssetsSold,
    StationDepositOther,
    // Expense entries
    MailPenalty,
    // Mono Begin
    BlackMarketPenalties,
    FrontierPenalties,
    NFSDPenalties,
    MedicalPenalties,
    // Mono End
    ShuttleRecordFees,
    StationWithdrawalPayroll,
    StationWithdrawalWorkOrder,
    StationWithdrawalSupplies,
    StationWithdrawalBounty,
    StationWithdrawalOther,
    // Utility values
    FirstExpense = MailPenalty,
}
