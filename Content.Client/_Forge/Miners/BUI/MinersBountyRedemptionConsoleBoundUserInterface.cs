using Content.Client._Forge.Miners.UI;
using Content.Shared._Forge.Miners.BUI;
using Content.Shared._Forge.Miners.Components;
using Content.Shared._Forge.Miners.Events;

namespace Content.Client._Forge.Miners.BUI;

public sealed class MinersBountyRedemptionConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MinersBountyRedemptionMenu? _menu;
    [ViewVariables]
    private EntityUid uid;

    public MinersBountyRedemptionConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        if (EntMan.TryGetComponent<MinersBountyRedemptionConsoleComponent>(owner, out var console))
            uid = owner;
    }

    protected override void Open()
    {
        base.Open();

        _menu = new MinersBountyRedemptionMenu();
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
        SendMessage(new MinersBountyRedemptionMessage());
    }

    // TODO: remove this, nothing to update
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MinersBountyRedemptionConsoleInterfaceState palletState)
            return;
    }
}
