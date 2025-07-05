using Content.Client._Forge.Mercenary.UI;
using Content.Shared._Forge.Mercenary.BUI;
using Content.Shared._Forge.Mercenary.Components;
using Content.Shared._Forge.Mercenary.Events;

namespace Content.Client._Forge.Mercenary.BUI;

public sealed class MercenaryBountyRedemptionConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MercenaryBountyRedemptionMenu? _menu;
    [ViewVariables]
    private EntityUid uid;

    public MercenaryBountyRedemptionConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        if (EntMan.TryGetComponent<MercenaryBountyRedemptionConsoleComponent>(owner, out var console))
            uid = owner;
    }

    protected override void Open()
    {
        base.Open();

        _menu = new MercenaryBountyRedemptionMenu();
        _menu.SellRequested += OnSell;
        _menu.OnClose += Close;

        _menu.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _menu?.Dispose();
        }
    }

    private void OnSell()
    {
        SendMessage(new MercenaryBountyRedemptionMessage());
    }

    // TODO: remove this, nothing to update
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MercenaryBountyRedemptionConsoleInterfaceState palletState)
            return;
    }
}
