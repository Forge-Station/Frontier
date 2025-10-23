using System.IO;
using Content.Shared._Forge.DiscordAuth;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Shared.Network;

namespace Content.Client._Forge.DiscordAuth;

public sealed class DiscordAuthManager
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IStateManager _state = default!;

    public string AuthLink = default!;
    public string ErrorMessage = default!;
    public Texture? QrCodeTexture;
    public const string DiscordServerLink = "https://discord.gg/q7ybZ5BaXW";

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgDiscordAuthRequired>(OnDiscordAuthRequired);
    }

    public void OnDiscordAuthRequired(MsgDiscordAuthRequired args)
    {
        AuthLink = args.Link;
        ErrorMessage = args.ErrorMessage;
        if (args.QrCodeBytes != null)
        {
            try
            {
                using var stream = new MemoryStream(args.QrCodeBytes);
                var texture = Texture.LoadFromPNGStream(stream);
                QrCodeTexture = texture;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load QR code: {ex.Message}");
            }
        }
        else
        {
            QrCodeTexture = null;
        }
        _state.RequestStateChange<DiscordAuthState>();
    }

    public void OnAuthSkip()
    {
        _net.ClientSendMessage(new MsgDiscordAuthSkip());
    }
}
