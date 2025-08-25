using Content.Shared._Forge.Contractor;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Forge.Contractor.Ui;

[UsedImplicitly]
public sealed class ContractsBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ContractsWindow? _window;

    public ContractsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ContractsWindow>();

        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _window.OnAcceptButtonPressed += contractId =>
            SendMessage(new AcceptContractMessage(contractId));

        _window.OnCompleteButtonPressed += contractId =>
            SendMessage(new CompleteContractMessage(contractId));

        _window.OnShopButtonPressed += () =>
            SendMessage(new OpenContractorShopMessage());

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window == null)
            return;

        if (message is ContractsUpdateStateMessage msg)
            _window?.UpdateState(msg);
    }
}
