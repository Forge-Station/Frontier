using Content.Shared._ADT.Modsuit.Events;
using Robust.Client.UserInterface;

namespace Content.Client._ADT.Modsuits.UI;

public sealed class ModSuitRadialBoundUserInterface : BoundUserInterface
{
    private readonly IEntityManager _entityManager;
    private ModSuitRadialMenu? _menu;

    public ModSuitRadialBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _entityManager = IoCManager.Resolve<IEntityManager>();
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ModSuitRadialMenu>();
        _menu.SetEntity(Owner);

        _menu.SendToggleClothingMessageAction += SendModSuitMessage;
        _menu.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RadialModBoundUiState)
            return;

        _menu?.RefreshUI();
    }

    private void SendModSuitMessage(EntityUid uid)
    {
        var message = new ToggleModSuitPartMessage(_entityManager.GetNetEntity(uid));
        SendPredictedMessage(message);
    }
}
