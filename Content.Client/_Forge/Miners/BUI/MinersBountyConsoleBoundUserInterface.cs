using Content.Client._Forge.Miners.UI;
using Content.Shared._Forge.Miners.Components;
using JetBrains.Annotations;

namespace Content.Client._Forge.Miners.BUI;

[UsedImplicitly]
public sealed class MinersBountyConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MinersBountyMenu? _menu;

    public MinersBountyConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new();

        _menu.OnClose += Close;

        _menu.OnLabelButtonPressed += id =>
        {
            SendMessage(new MinersBountyAcceptMessage(id));
        };

        _menu.OnSkipButtonPressed += id =>
        {
            SendMessage(new MinersBountySkipMessage(id));
        };

        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState message)
    {
        base.UpdateState(message);

        if (message is not MinersBountyConsoleState state)
            return;

        _menu?.UpdateEntries(state.Bounties, state.UntilNextSkip);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Dispose();
    }
}
