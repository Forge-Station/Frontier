using Content.Server.EUI;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Shared._Forge.Contractor;
using Content.Shared.Eui;
using Content.Shared.Popups;

namespace Content.Server._Forge.Contractor.UI;

/// <summary>
/// Logic for the confirmation window
/// </summary>
public sealed class ContractorEui(EntityUid contractor, ContractorRuleSystem contractorSystem, PopupSystem popup) : BaseEui
{
    public override EuiStateBase GetNewState()
        => new ContractorState();

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is ContractorAcceptedMessage accepted)
        {
            if (accepted.Accepted)
            {
                contractorSystem.OnContractAccepted(contractor);
                popup.PopupEntity(
                    Loc.GetString("contract-accepted"),
                    contractor,
                    PopupType.Medium);
            }
            else
            {
                contractorSystem.OnContractRejected();
                popup.PopupEntity(
                    Loc.GetString("contract-rejected"),
                    contractor,
                    PopupType.MediumCaution);
            }
        }

        Close();
    }
}
