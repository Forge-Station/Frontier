using System.Numerics;
using Content.Client.Eui;
using Content.Shared._Forge.Contractor;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Forge.Contractor.Ui;

[UsedImplicitly]
public sealed class ContractorEui : BaseEui
{
    private readonly ContractorAcceptWindow _window;

    public ContractorEui()
    {
        _window = new ContractorAcceptWindow();

        _window.OnDeny += () =>
        {
            SendMessage(new ContractorAcceptedMessage(false));
            _window.Close();
        };

        _window.OnClose += () => SendMessage(new ContractorAcceptedMessage(false));

        _window.OnAccept += () =>
        {
            SendMessage(new ContractorAcceptedMessage(true));
            _window.Close();
        };
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCenteredAt(new Vector2(0.5f, 0.75f));
    }

    public override void Closed()
    {
        _window.Close();
    }
}
