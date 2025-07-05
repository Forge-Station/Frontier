using Content.Client._Forge.Mercenary.UI;
using Content.Shared._Forge.Mercenary.Components;
using JetBrains.Annotations;

namespace Content.Client._Forge.Mercenary.BUI;

[UsedImplicitly]
public sealed class MercenaryBountyConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MercenaryBountyMenu? _menu;

    public MercenaryBountyConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new();

        _menu.OnClose += Close;

        _menu.OnLabelButtonPressed += id =>
        {
            SendMessage(new MercenaryBountyAcceptMessage(id));
        };

        _menu.OnSkipButtonPressed += id =>
        {
            SendMessage(new MercenaryBountySkipMessage(id));
        };

        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState message)
    {
        base.UpdateState(message);

        if (message is not MercenaryBountyConsoleState state)
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
